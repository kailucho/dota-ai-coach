"""Export real numeric parity fixtures for the three C#-consumed ONNX
models (win-probability-live, draft-baseline, item-ranker), closing the
gap ADR 0023 Decision 6 flagged: no fixture recorded actual input feature
vectors alongside expected outputs, so tests/DotaAiCoach.MlInference.Tests
could only assert structural correctness, not true numeric parity.

Reuses export_onnx.py's existing sampling approach (_fixed_test_inputs'
edge-case categories: all-zero/missing-feature, extreme large positive/
negative, mid-boundary, representative rows) rather than re-deriving a
different one, extended here to also draw real deterministic rows from
each model's own val split (not just synthetic edge cases), and to
serialize full named input feature dicts (not just raw arrays) so a C#
test can reconstruct an equivalent call.

For each of the 3 models, per test case, records:
- input feature dict (named per the model's onnx_export_metadata.json
  feature_order -- exact key names used are the model's own Python
  feature-column names, since the fixture's job is Python<->ONNX/C#
  numeric parity, not a second copy of the Domain type mapping already
  documented in WinProbabilityPredictor.cs/DraftScorePredictor.cs/
  ItemRankPredictor.cs's own comments).
- expected_raw_output: the pre-export (pickled) model's own prediction.
- expected_calibrated_output: isotonic-calibrated version, where
  applicable (win-probability-live, draft-baseline only).
- expected_onnx_output: running model.onnx directly via onnxruntime
  InferenceSession (not trusting the pre-export model's number).
- max_abs_diff between pre-export and ONNX raw outputs, measured, not
  assumed.

Run: .venv/Scripts/python.exe scripts/export_parity_fixtures.py
"""

from __future__ import annotations

import json
import pickle
import sys
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import onnxruntime as ort
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

from dota_training.config import DATASETS_DIR, MODELS_DIR  # noqa: E402
from dota_training.models.draft_baseline import compute_draft_features  # noqa: E402
from dota_training.models.export_onnx import _fixed_test_inputs  # noqa: E402

WIN_PROB_MODEL_ID = "win-probability-live-v1.0-20260810T034501Z"
DRAFT_MODEL_ID = "draft-baseline-v1.0-20260810T032042Z"
ITEM_RANKER_MODEL_ID = "item-ranker-v1.0-20260810T203622Z"

WIN_PROB_DATASET_DIR = DATASETS_DIR / "ranked-ad-2760-20260810"
DRAFT_DATASET_DIR = DATASETS_DIR / "draft-ranked-ad-2760-20260810"
ITEM_EVENTS_DATASET_DIR = DATASETS_DIR / "item-events-2780-20260810"

FIXTURES_DIR = Path(__file__).resolve().parents[1] / "tests" / "fixtures" / "onnx_parity"

EDGE_CASE_NAMES = [
    "edge_all_zero_missing_feature_fill",
    "edge_extreme_large_positive",
    "edge_extreme_large_negative",
    "edge_mid_boundary_0_5",
    "representative_random_row_1",
    "representative_random_row_2",
]


def _load_bundle(model_dir: Path) -> dict:
    with open(model_dir / "model.pkl", "rb") as f:
        return pickle.load(f)


def _load_calibrator(model_dir: Path):
    path = model_dir / "calibrator.pkl"
    if not path.exists():
        return None
    with open(path, "rb") as f:
        return pickle.load(f)


def _onnx_session(model_dir: Path) -> ort.InferenceSession:
    return ort.InferenceSession(str(model_dir / "model.onnx"), providers=["CPUExecutionProvider"])


# ---------------------------------------------------------------------------
# Win Probability (live) + Draft baseline: shared logic, both are
# StandardScaler+LogisticRegression pipelines exported via skl2onnx with
# zipmap disabled (probabilities[:, 1] = P(radiant_win=1)).
# ---------------------------------------------------------------------------


def _sklearn_pipeline_case(
    case_name: str,
    description: str,
    feature_dict: dict,
    feature_columns: list[str],
    scaler,
    logreg,
    calibrator,
    sess: ort.InferenceSession,
) -> dict:
    x = np.array([[feature_dict[c] for c in feature_columns]], dtype=np.float32)

    raw_proba = float(logreg.predict_proba(scaler.transform(x))[:, 1][0])
    calibrated = float(calibrator.predict([raw_proba])[0]) if calibrator is not None else None

    onnx_out = sess.run(None, {"input": x})
    onnx_probs = onnx_out[1]
    onnx_proba = float(onnx_probs[0][1] if onnx_probs.ndim == 2 else onnx_probs[1])

    return {
        "case_name": case_name,
        "description": description,
        "input": feature_dict,
        "expected_raw_output": raw_proba,
        "expected_calibrated_output": calibrated,
        "expected_onnx_output": onnx_proba,
        "raw_vs_onnx_abs_diff": abs(raw_proba - onnx_proba),
    }


def _edge_case_feature_dicts(feature_columns: list[str]) -> list[tuple[str, str, dict]]:
    """Reuses export_onnx._fixed_test_inputs' exact 6 fixed rows (same
    edge-case categories: all-zero, extreme-large-positive,
    extreme-large-negative, mid-boundary, 2 representative random rows),
    turned into named dicts here instead of raw arrays so the fixture
    records the actual input vector, closing ADR 0023 Decision 6's gap."""
    arr = _fixed_test_inputs(len(feature_columns))
    descriptions = [
        "All features zero -- the missing-feature/zero-fill convention used at training and inference time (df.fillna(0.0)).",
        "All features set to an extreme large positive value (1e6) -- numerical-stability edge case.",
        "All features set to an extreme large negative value (-1e6) -- numerical-stability edge case.",
        "All features set to a boundary-ish mid value (0.5) -- boundary-value edge case.",
        "Representative random row 1 (uniform[-1000, 1000], seed 42) -- typical-magnitude sanity case.",
        "Representative random row 2 (uniform[-1000, 1000], seed 42) -- typical-magnitude sanity case.",
    ]
    out = []
    for name, desc, row in zip(EDGE_CASE_NAMES, descriptions, arr):
        out.append((name, desc, dict(zip(feature_columns, [float(v) for v in row]))))
    return out


def export_win_probability_fixtures() -> dict:
    model_dir = MODELS_DIR / WIN_PROB_MODEL_ID
    bundle = _load_bundle(model_dir)
    feature_columns = bundle["feature_columns"]
    scaler, logreg = bundle["scaler"], bundle["model"]
    calibrator = _load_calibrator(model_dir)
    sess = _onnx_session(model_dir)

    cases = []
    for name, desc, fdict in _edge_case_feature_dicts(feature_columns):
        cases.append(_sklearn_pipeline_case(name, desc, fdict, feature_columns, scaler, logreg, calibrator, sess))

    # Real deterministic rows from val split, not just synthetic edge cases.
    val_path = WIN_PROB_DATASET_DIR / "val.parquet"
    if val_path.exists():
        val_df = pd.read_parquet(val_path)
        non_fb_cols = [c for c in feature_columns if c != "first_blood_radiant"]
        picked_idx = list(range(0, min(10, len(val_df))))
        for i, idx in enumerate(picked_idx):
            row = val_df.iloc[idx]
            fdict = {}
            for c in non_fb_cols:
                v = row[c]
                fdict[c] = float(v) if pd.notna(v) else 0.0
            if "first_blood_radiant" in feature_columns:
                fb = row.get("first_blood_radiant")
                fdict["first_blood_radiant"] = 1.0 if fb is True else (0.0 if fb is False else 0.5)
            cases.append(
                _sklearn_pipeline_case(
                    f"real_val_row_{i}",
                    f"Real row {idx} from val.parquet (deterministic, first {len(picked_idx)} rows by positional index).",
                    fdict,
                    feature_columns,
                    scaler,
                    logreg,
                    calibrator,
                    sess,
                )
            )

    max_abs_diff = max(c["raw_vs_onnx_abs_diff"] for c in cases)
    return {
        "metadata": {
            "source_model_id": WIN_PROB_MODEL_ID,
            "feature_order": feature_columns,
            "n_cases": len(cases),
            "max_abs_diff_raw_vs_onnx_achieved": max_abs_diff,
            "generated_at": datetime.now(timezone.utc).isoformat(),
            "note": "expected_calibrated_output uses the model's own IsotonicRegression calibrator.pkl (fit on calib split) applied to expected_raw_output -- not the ONNX output, since ONNX has no calibration embedded (per onnx_export_metadata.json calibration_included_in_graph=false).",
        },
        "cases": cases,
    }


def export_draft_baseline_fixtures() -> dict:
    model_dir = MODELS_DIR / DRAFT_MODEL_ID
    bundle = _load_bundle(model_dir)
    feature_columns = bundle["feature_columns"]
    scaler, logreg = bundle["scaler"], bundle["model"]
    calibrator = _load_calibrator(model_dir)
    sess = _onnx_session(model_dir)

    cases = []
    for name, desc, fdict in _edge_case_feature_dicts(feature_columns):
        cases.append(_sklearn_pipeline_case(name, desc, fdict, feature_columns, scaler, logreg, calibrator, sess))

    val_path = DRAFT_DATASET_DIR / "val.parquet"
    if val_path.exists():
        val_df = pd.read_parquet(val_path)
        features_df = compute_draft_features(val_df, bundle["hero_winrates"], bundle["matchup_matrix"], bundle["overall_winrate"])
        picked_idx = list(range(0, min(10, len(features_df))))
        for i, idx in enumerate(picked_idx):
            row = features_df.iloc[idx]
            fdict = {c: float(row[c]) for c in feature_columns}
            cases.append(
                _sklearn_pipeline_case(
                    f"real_val_row_{i}",
                    f"Real row {idx} from val.parquet, features recomputed via compute_draft_features (deterministic, first {len(picked_idx)} rows by positional index).",
                    fdict,
                    feature_columns,
                    scaler,
                    logreg,
                    calibrator,
                    sess,
                )
            )

    max_abs_diff = max(c["raw_vs_onnx_abs_diff"] for c in cases)
    return {
        "metadata": {
            "source_model_id": DRAFT_MODEL_ID,
            "feature_order": feature_columns,
            "n_cases": len(cases),
            "max_abs_diff_raw_vs_onnx_achieved": max_abs_diff,
            "generated_at": datetime.now(timezone.utc).isoformat(),
            "note": "expected_calibrated_output uses the model's own IsotonicRegression calibrator.pkl (fit on calib split) applied to expected_raw_output.",
        },
        "cases": cases,
    }


# ---------------------------------------------------------------------------
# Item ranker: LightGBM LambdaMART, no calibration, single raw score output.
# ---------------------------------------------------------------------------


def export_item_ranker_fixtures() -> dict:
    model_dir = MODELS_DIR / ITEM_RANKER_MODEL_ID
    bundle = _load_bundle(model_dir)
    feature_columns = bundle["feature_columns"]
    ranker = bundle["model"]
    sess = _onnx_session(model_dir)
    input_name = sess.get_inputs()[0].name

    cases = []
    for name, desc, fdict in _edge_case_feature_dicts(feature_columns):
        x_df = pd.DataFrame([fdict], columns=feature_columns)
        raw_score = float(ranker.predict(x_df)[0])
        onnx_out = sess.run(None, {input_name: x_df.values.astype(np.float32)})
        onnx_score = float(np.asarray(onnx_out[0]).reshape(-1)[0])
        cases.append(
            {
                "case_name": name,
                "description": desc,
                "input": fdict,
                "expected_raw_output": raw_score,
                "expected_calibrated_output": None,
                "expected_onnx_output": onnx_score,
                "raw_vs_onnx_abs_diff": abs(raw_score - onnx_score),
            }
        )

    # Real deterministic rows: build candidate rows from val split via the
    # model's own popularity tables, pick the first 10 candidate rows by
    # position (not random) so the fixture is reproducible.
    val_path = ITEM_EVENTS_DATASET_DIR / "val.parquet"
    if val_path.exists():
        from dota_training.models.item_ranker import build_candidate_rows, load_avg_rank_tier_by_match

        val_df = pd.read_parquet(val_path)
        tables = bundle["tables"]
        all_items = bundle["all_items"]
        avg_rank_tier_by_match = load_avg_rank_tier_by_match()
        avg_rank_tier_fill = bundle.get("avg_rank_tier_fill", 0.0)
        candidates = build_candidate_rows(val_df, all_items, tables, avg_rank_tier_by_match, avg_rank_tier_fill)
        picked_idx = list(range(0, min(10, len(candidates))))
        for i, idx in enumerate(picked_idx):
            row = candidates.iloc[idx]
            fdict = {c: float(row[c]) for c in feature_columns}
            x_df = pd.DataFrame([fdict], columns=feature_columns)
            raw_score = float(ranker.predict(x_df)[0])
            onnx_out = sess.run(None, {input_name: x_df.values.astype(np.float32)})
            onnx_score = float(np.asarray(onnx_out[0]).reshape(-1)[0])
            cases.append(
                {
                    "case_name": f"real_val_row_{i}",
                    "description": f"Real candidate row {idx} from val.parquet's build_candidate_rows output (deterministic, first {len(picked_idx)} rows by positional index).",
                    "input": fdict,
                    "expected_raw_output": raw_score,
                    "expected_calibrated_output": None,
                    "expected_onnx_output": onnx_score,
                    "raw_vs_onnx_abs_diff": abs(raw_score - onnx_score),
                }
            )

    max_abs_diff = max(c["raw_vs_onnx_abs_diff"] for c in cases)
    return {
        "metadata": {
            "source_model_id": ITEM_RANKER_MODEL_ID,
            "feature_order": feature_columns,
            "n_cases": len(cases),
            "max_abs_diff_raw_vs_onnx_achieved": max_abs_diff,
            "generated_at": datetime.now(timezone.utc).isoformat(),
            "note": "No calibration step for this model (ranking task, not a probability) -- expected_calibrated_output is always null.",
        },
        "cases": cases,
    }


def main() -> None:
    FIXTURES_DIR.mkdir(parents=True, exist_ok=True)

    win_prob = export_win_probability_fixtures()
    (FIXTURES_DIR / "win_probability_live_parity.json").write_text(json.dumps(win_prob, indent=2), encoding="utf-8")
    print(f"win-probability-live: {win_prob['metadata']['n_cases']} cases, max_abs_diff={win_prob['metadata']['max_abs_diff_raw_vs_onnx_achieved']:.3e}")

    draft = export_draft_baseline_fixtures()
    (FIXTURES_DIR / "draft_baseline_parity.json").write_text(json.dumps(draft, indent=2), encoding="utf-8")
    print(f"draft-baseline: {draft['metadata']['n_cases']} cases, max_abs_diff={draft['metadata']['max_abs_diff_raw_vs_onnx_achieved']:.3e}")

    item_ranker = export_item_ranker_fixtures()
    (FIXTURES_DIR / "item_ranker_parity.json").write_text(json.dumps(item_ranker, indent=2), encoding="utf-8")
    print(f"item-ranker: {item_ranker['metadata']['n_cases']} cases, max_abs_diff={item_ranker['metadata']['max_abs_diff_raw_vs_onnx_achieved']:.3e}")


if __name__ == "__main__":
    main()
