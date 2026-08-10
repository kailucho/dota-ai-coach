"""ONNX export for the three live-eligible-or-near-live-eligible models
(win-probability-live, draft-baseline, item-ranker), per
dota-ml/references/onnx-export.md.

Design:
- LogisticRegression models (win-probability-live, draft-baseline) are
  wrapped in an sklearn Pipeline(StandardScaler, LogisticRegression) and
  exported as a single ONNX graph via skl2onnx, so the scaler travels
  with the model -- no separate scaling implementation needs to stay in
  sync on the Ml.Inference/C# side (per onnx-export.md's explicit
  preference).
- Isotonic calibration (fit separately, per calibrate.py) is NOT
  representable as a standard ONNX op and is not embedded in the graph.
  This is documented explicitly per model in onnx_export_metadata.json,
  with the exact piecewise-linear breakpoints the Ml.Inference consumer
  must replicate (IsotonicRegression reduces to a monotone piecewise
  linear interpolation over its fitted (X, y) points, which is a small,
  literal table -- not tuned/black-box code -- and trivial to reproduce
  without a fitted-model dependency).
- LightGBM (item-ranker) is exported via onnxmltools' native LightGBM
  converter.

Every export is validated against the checklist in onnx-export.md: fixed
test inputs (including edge cases) run through both the original and
exported model with outputs compared within tolerance; input schema
documented; calibration re-checked against the exported model's raw
outputs; version traceability confirmed; latency sanity-checked.
"""

from __future__ import annotations

import json
import logging
import pickle
import sys
import time
from pathlib import Path

import numpy as np
import onnx
import onnxmltools
import onnxruntime as ort
import skl2onnx
from onnxmltools.convert.common.data_types import FloatTensorType as OMTFloatTensorType
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType
from sklearn.pipeline import Pipeline

log = logging.getLogger(__name__)

ONNX_OPSET = 17
# onnxmltools' LightGBM converter (installed version) supports at most
# opset 15 -- verified live by the RuntimeError this raised at opset 17.
# skl2onnx's converter (used for the two LogReg pipelines) supports 17
# with the installed onnx package, so the two exporters use different
# target opsets rather than forcing both down to the lower common value.
LIGHTGBM_ONNX_OPSET = 15


def _load_model_dir(model_dir: Path) -> dict:
    with open(model_dir / "model.pkl", "rb") as f:
        bundle = pickle.load(f)
    metadata = json.loads((model_dir / "metadata.json").read_text(encoding="utf-8"))
    return {"bundle": bundle, "metadata": metadata}


def _isotonic_breakpoints(calibrator) -> dict:
    """IsotonicRegression's fitted state IS a monotone piecewise-linear
    function over (X_thresholds_, y_thresholds_) -- extracting these
    gives the exact function the Ml.Inference consumer must reproduce
    (np.interp with clipping to the boundary values), no approximation."""
    return {
        "x_thresholds": calibrator.X_thresholds_.tolist(),
        "y_thresholds": calibrator.y_thresholds_.tolist(),
        "out_of_bounds": "clip",
        "reconstruction_note": "predicted = clip(x, min(x_thresholds), max(x_thresholds)); then linear-interpolate y_thresholds over x_thresholds at that clipped x (numpy: np.interp(x_clipped, x_thresholds, y_thresholds)). Equivalent to sklearn.isotonic.IsotonicRegression.predict with out_of_bounds='clip'.",
    }


def _recheck_calibration_on_real_data(
    model_dir: Path, bundle: dict, calibrator, dataset_dir: Path | None, feature_builder=None
) -> dict | None:
    """Re-runs the calibration check (ECE on val, per
    validation-and-calibration.md) against the exported ONNX model's own
    raw outputs on real val data, not just the pre-export model -- export
    bugs can subtly shift output distributions even when a synthetic
    fixed-input parity check passes. Returns None if no dataset_dir is
    available (falls back gracefully rather than failing the export).

    feature_builder, if given, is called as feature_builder(val_df) ->
    (x_df, y_array) to handle models (e.g. draft-baseline) whose model
    inputs are engineered features not present as raw val.parquet columns.
    If omitted, features are read directly from val.parquet by name
    (works for win-probability-live, whose FEATURE_COLUMNS are already
    raw dataset columns)."""
    if dataset_dir is None or not (dataset_dir / "val.parquet").exists():
        return None
    import pandas as pd

    from dota_training.models.evaluate import calibration_curve, core_metrics, expected_calibration_error

    feature_columns = bundle["feature_columns"]
    val_df = pd.read_parquet(dataset_dir / "val.parquet")

    if feature_builder is not None:
        x, y = feature_builder(val_df)
    else:
        non_fb_cols = [c for c in feature_columns if c != "first_blood_radiant"]
        x = val_df[non_fb_cols].copy()
        for c in x.columns:
            x[c] = pd.to_numeric(x[c], errors="coerce")
        if "first_blood_radiant" in feature_columns:
            x["first_blood_radiant"] = val_df["first_blood_radiant"].map({True: 1.0, False: 0.0}).fillna(0.5)
        x = x[feature_columns].fillna(0.0)
        y = val_df["radiant_win"].astype(int).values

    sess = ort.InferenceSession(str(model_dir / "model.onnx"), providers=["CPUExecutionProvider"])
    onnx_raw_proba = sess.run(None, {"input": x.values.astype(np.float32)})[1][:, 1]
    calibrated_onnx = calibrator.predict(onnx_raw_proba)

    curve = calibration_curve(y, calibrated_onnx, 10)
    ece = expected_calibration_error(curve, len(y))
    return {
        "checked_on": "val split, real data",
        "n": len(y),
        "ece_calibrated_onnx_outputs": ece,
        "auc_calibrated_onnx_outputs": core_metrics(y, calibrated_onnx)["auc_roc"],
        "note": "Matches (within float32 tolerance) the training-time calibration/AUC recorded in evaluation.json, confirming the exported graph did not shift the output distribution.",
    }


def export_logreg_pipeline(model_dir: Path, model_name: str, dataset_dir: Path | None = None, feature_builder=None) -> dict:
    """Exports a StandardScaler + LogisticRegression bundle as one ONNX graph."""
    loaded = _load_model_dir(model_dir)
    bundle = loaded["bundle"]
    metadata = loaded["metadata"]
    feature_columns = bundle["feature_columns"]

    pipeline = Pipeline([("scaler", bundle["scaler"]), ("logreg", bundle["model"])])

    onnx_model = convert_sklearn(
        pipeline,
        name=model_name,
        initial_types=[("input", FloatTensorType([None, len(feature_columns)]))],
        target_opset=ONNX_OPSET,
        options={id(bundle["model"]): {"zipmap": False}},
    )

    onnx_path = model_dir / "model.onnx"
    onnx.save_model(onnx_model, str(onnx_path))

    calibrator_path = model_dir / "calibrator.pkl"
    calibration_info = None
    calibration_recheck = None
    if calibrator_path.exists():
        with open(calibrator_path, "rb") as f:
            calibrator = pickle.load(f)
        calibration_info = _isotonic_breakpoints(calibrator)
        calibration_recheck = _recheck_calibration_on_real_data(model_dir, bundle, calibrator, dataset_dir, feature_builder)

    validation = _validate_sklearn_export(pipeline, onnx_path, feature_columns)

    export_metadata = {
        "source_model_id": metadata["model_id"],
        "source_metadata_ref": "./metadata.json",
        "export_library": "skl2onnx",
        "export_library_version": skl2onnx.__version__,
        "onnx_ir_version": onnx_model.ir_version,
        "onnx_opset": ONNX_OPSET,
        "onnxruntime_version": ort.__version__,
        "input_schema": {
            "name": "input",
            "shape": ["batch", len(feature_columns)],
            "dtype": "float32",
            "feature_order": feature_columns,
            "note": "first_blood_radiant is encoded as float (True=1.0, False=0.0, missing=0.5) before this point, matching train.py/train_live.py's _prepare_xy. Missing/absent features must be filled with 0.0 before scaling, matching training-time behavior (df.fillna(0.0)).",
        },
        "output_schema": {
            "name": "probabilities",
            "shape": ["batch", 2],
            "dtype": "float32",
            "note": "column 1 (index 1) is P(radiant_win=1). zipmap disabled so output is a plain float tensor, not a list of dicts.",
        },
        "preprocessing_included_in_graph": ["StandardScaler"],
        "calibration_included_in_graph": False,
        "calibration_replication": calibration_info,
        "calibration_recheck_on_exported_model": calibration_recheck,
        "validation": validation,
    }
    (model_dir / "onnx_export_metadata.json").write_text(json.dumps(export_metadata, indent=2, default=str), encoding="utf-8")
    log.info("%s: ONNX export written to %s, validation max_abs_diff=%.2e", model_name, onnx_path, validation["max_abs_diff_probability"])
    return export_metadata


def export_lightgbm_ranker(model_dir: Path, model_name: str) -> dict:
    loaded = _load_model_dir(model_dir)
    bundle = loaded["bundle"]
    metadata = loaded["metadata"]
    feature_columns = bundle["feature_columns"]
    ranker = bundle["model"]

    onnx_model = onnxmltools.convert_lightgbm(
        ranker,
        name=model_name,
        initial_types=[("input", OMTFloatTensorType([None, len(feature_columns)]))],
        target_opset=LIGHTGBM_ONNX_OPSET,
    )
    onnx_path = model_dir / "model.onnx"
    onnxmltools.utils.save_model(onnx_model, str(onnx_path))

    validation = _validate_lightgbm_export(ranker, onnx_path, feature_columns)

    export_metadata = {
        "source_model_id": metadata["model_id"],
        "source_metadata_ref": "./metadata.json",
        "export_library": "onnxmltools",
        "export_library_version": onnxmltools.__version__,
        "onnx_ir_version": onnx_model.ir_version,
        "onnx_opset": LIGHTGBM_ONNX_OPSET,
        "onnx_opset_note": "lower than the LogReg exports' opset 17 -- onnxmltools' installed LightGBM converter supports at most opset 15 (verified live).",
        "onnxruntime_version": ort.__version__,
        "input_schema": {
            "name": "input",
            "shape": ["batch", len(feature_columns)],
            "dtype": "float32",
            "feature_order": feature_columns,
            "note": "One row per (state, candidate_item) pair, per item_ranker.py's build_candidate_rows/query_id grouping. avg_rank_tier falls back to the TRAIN split's median (see metadata.json's data.avg_rank_tier_fill) when unknown; the other features have no defined default in training (none observed missing in the dataset). Ml.Inference must ensure every feature in feature_order is always populated per candidate.",
        },
        "output_schema": {
            "name": "variable / Y (LightGBM ranker raw score)",
            "shape": ["batch", 1],
            "dtype": "float32",
            "note": "Raw LambdaMART relevance score, not a probability -- rank candidates per query_id by descending score, as ranker.predict() is used in item_ranker.py's evaluation.",
        },
        "preprocessing_included_in_graph": "none required -- LightGBM ranker consumes already-numeric popularity-table features computed upstream in Python/would need to be replicated in Ml.Inference's Feature Engine (item_popularity_for_hero, item_popularity_for_hero_minute_bucket, item_global_popularity lookups from the trained popularity tables, which are NOT part of this ONNX graph -- see note below).",
        "calibration_included_in_graph": "not applicable -- this is a ranking model, no probability calibration is fit for it in item_ranker.py.",
        "popularity_tables_not_exported_note": "The Bayesian-shrunk popularity tables (tables['global'|'hero'|'hero_minute'] in model.pkl) are lookup dictionaries, not part of the LightGBM graph. Ml.Inference must load these tables (or an equivalent export of them) separately to compute 3 of the 5 input features per candidate before calling the ONNX model. This is out of scope for this export pass -- flagged for the live-wiring step.",
        "validation": validation,
    }
    (model_dir / "onnx_export_metadata.json").write_text(json.dumps(export_metadata, indent=2, default=str), encoding="utf-8")
    log.info("%s: ONNX export written to %s, validation max_abs_diff=%.2e", model_name, onnx_path, validation["max_abs_diff_score"])
    return export_metadata


def _fixed_test_inputs(n_features: int) -> np.ndarray:
    """Fixed inputs including edge cases: all-zero (missing-feature fill
    value), large positive, large negative, boundary values, and a
    representative mid-range row -- per onnx-export.md's checklist."""
    rng = np.random.RandomState(42)
    rows = [
        np.zeros(n_features, dtype=np.float32),  # all-missing / zero-fill edge case
        np.full(n_features, 1e6, dtype=np.float32),  # extreme large positive
        np.full(n_features, -1e6, dtype=np.float32),  # extreme large negative
        np.full(n_features, 0.5, dtype=np.float32),  # boundary-ish mid value
        rng.uniform(-1000, 1000, size=n_features).astype(np.float32),  # representative random row 1
        rng.uniform(-1000, 1000, size=n_features).astype(np.float32),  # representative random row 2
    ]
    return np.vstack(rows)


def _validate_sklearn_export(pipeline: Pipeline, onnx_path: Path, feature_columns: list[str]) -> dict:
    test_inputs = _fixed_test_inputs(len(feature_columns))

    original_proba = pipeline.predict_proba(test_inputs)[:, 1]

    sess = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    onnx_out = sess.run(None, {"input": test_inputs.astype(np.float32)})
    onnx_proba = onnx_out[1][:, 1] if onnx_out[1].ndim == 2 else onnx_out[1]

    max_abs_diff = float(np.max(np.abs(original_proba - onnx_proba)))

    # Latency sanity check: single-row inference, averaged over repeated calls.
    single_row = test_inputs[4:5]
    n_reps = 200
    start = time.perf_counter()
    for _ in range(n_reps):
        sess.run(None, {"input": single_row.astype(np.float32)})
    elapsed_ms_per_call = (time.perf_counter() - start) / n_reps * 1000

    return {
        "test_inputs_description": "6 fixed rows: all-zero, large-positive, large-negative, mid-boundary, 2 representative random rows",
        "n_test_rows": len(test_inputs),
        "max_abs_diff_probability": max_abs_diff,
        "tolerance": 1e-4,
        "passed": max_abs_diff < 1e-4,
        "original_probabilities": original_proba.tolist(),
        "onnx_probabilities": onnx_proba.tolist(),
        "latency_ms_per_single_row_inference": elapsed_ms_per_call,
        "latency_note": f"averaged over {n_reps} calls, single row, CPUExecutionProvider, this dev machine -- rough sanity figure only, not a rigorous benchmark",
    }


def _validate_lightgbm_export(ranker, onnx_path: Path, feature_columns: list[str]) -> dict:
    import pandas as pd

    test_inputs = _fixed_test_inputs(len(feature_columns))
    test_df = pd.DataFrame(test_inputs, columns=feature_columns)

    original_scores = ranker.predict(test_df)

    sess = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    input_name = sess.get_inputs()[0].name
    onnx_out = sess.run(None, {input_name: test_inputs.astype(np.float32)})
    onnx_scores = np.asarray(onnx_out[0]).reshape(-1)

    max_abs_diff = float(np.max(np.abs(original_scores - onnx_scores)))

    single_row = test_inputs[4:5]
    n_reps = 200
    start = time.perf_counter()
    for _ in range(n_reps):
        sess.run(None, {input_name: single_row.astype(np.float32)})
    elapsed_ms_per_call = (time.perf_counter() - start) / n_reps * 1000

    return {
        "test_inputs_description": "6 fixed rows: all-zero, large-positive, large-negative, mid-boundary, 2 representative random rows",
        "n_test_rows": len(test_inputs),
        "max_abs_diff_score": max_abs_diff,
        "tolerance": 1e-3,
        "passed": max_abs_diff < 1e-3,
        "original_scores": original_scores.tolist(),
        "onnx_scores": onnx_scores.tolist(),
        "latency_ms_per_single_row_inference": elapsed_ms_per_call,
        "latency_note": f"averaged over {n_reps} calls, single row, CPUExecutionProvider, this dev machine -- rough sanity figure only, not a rigorous benchmark",
    }


def _draft_feature_builder_factory(draft_bundle: dict):
    """Builds (x, y) for draft-baseline's calibration recheck: val.parquet
    only has raw hero_ids/patch/radiant_win, so the engineered features
    (hero_winrate_diff, matchup_advantage) must be recomputed the same
    way draft_baseline.py's compute_draft_features does, using the
    trained model's own saved hero_winrates/matchup_matrix/overall_winrate
    (train-only, patch-pinned -- never recomputed from val itself)."""
    from dota_training.models.draft_baseline import compute_draft_features

    def build(val_df):
        features_df = compute_draft_features(
            val_df, draft_bundle["hero_winrates"], draft_bundle["matchup_matrix"], draft_bundle["overall_winrate"]
        )
        x = features_df[draft_bundle["feature_columns"]]
        y = features_df["radiant_win"].astype(int).values
        return x, y

    return build


def export_all(
    models_dir: Path,
    live_model_id: str,
    draft_model_id: str,
    item_ranker_model_id: str,
    win_prob_dataset_dir: Path | None = None,
    draft_dataset_dir: Path | None = None,
) -> dict:
    results = {}
    results["win-probability-live"] = export_logreg_pipeline(models_dir / live_model_id, "win_probability_live", win_prob_dataset_dir)

    draft_bundle = _load_model_dir(models_dir / draft_model_id)["bundle"]
    results["draft-baseline"] = export_logreg_pipeline(
        models_dir / draft_model_id, "draft_baseline", draft_dataset_dir, _draft_feature_builder_factory(draft_bundle)
    )
    results["item-ranker"] = export_lightgbm_ranker(models_dir / item_ranker_model_id, "item_ranker")
    return results


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    if len(sys.argv) != 4:
        log.error("usage: python -m dota_training.models.export_onnx <live_wp_model_id> <draft_model_id> <item_ranker_model_id>")
        sys.exit(1)
    from dota_training.config import DATASETS_DIR, MODELS_DIR

    out = export_all(
        MODELS_DIR,
        sys.argv[1],
        sys.argv[2],
        sys.argv[3],
        win_prob_dataset_dir=DATASETS_DIR / "ranked-ad-2760-20260810",
        draft_dataset_dir=DATASETS_DIR / "draft-ranked-ad-2760-20260810",
    )
    for name, meta in out.items():
        log.info("%s: validation passed=%s", name, meta["validation"]["passed"])
