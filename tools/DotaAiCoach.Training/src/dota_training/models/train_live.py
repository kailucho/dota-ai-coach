"""LIVE_ALLOWED-restricted retrain of the Win Probability model.

Sibling module to train.py (kept intact/buildable as the documented
POSTGAME_ONLY research baseline per docs/adr/0017/0018/0021) rather than a
parameterization of it. Chosen over a `feature_set` flag on train.py
because the two variants diverge in more than the feature list -- the
live variant's metadata/fairplay block, model_id prefix, and (once this
lands) its ONNX export path are all meaningfully different artifacts, not
alternate configurations of one artifact. Reuses train.py's shared
helpers (_prepare_xy shape, calibrate.py, evaluate.py) to avoid
duplicating the actual training discipline.

Feature set verified against features_spec.yaml (current contents, this
run): only features classified LIVE_ALLOWED or PUBLIC_PREFLIGHT are
included. Excluded, all POSTGAME_ONLY: lh_diff_at_T, gold_gini_radiant,
gold_gini_dire, max_hero_gold_diff, teamfights_won_diff,
key_item_count_diff. See docs/adr/0022.
"""

from __future__ import annotations

import json
import logging
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

import lightgbm as lgb
import numpy as np
import pandas as pd
import sklearn
from sklearn.linear_model import LogisticRegression
from sklearn.preprocessing import StandardScaler

from dota_training.config import (
    ABLATION_FEATURE,
    CALIBRATION_BINS,
    ECE_ISSUE_THRESHOLD,
    MODELS_DIR,
    RANDOM_SEED,
)
from dota_training.models.calibrate import apply_calibrator, fit_calibrator
from dota_training.models.evaluate import (
    core_metrics,
    calibration_curve,
    domain_sanity_checks,
    expected_calibration_error,
    segment_by_hero,
    segment_by_patch,
    segment_by_rank_tier,
    segment_by_timestamp_bucket,
)

log = logging.getLogger(__name__)

# LIVE_ALLOWED / PUBLIC_PREFLIGHT subset of features_spec.yaml only.
# POSTGAME_ONLY features (lh_diff_at_T, gold_gini_radiant, gold_gini_dire,
# max_hero_gold_diff, teamfights_won_diff, key_item_count_diff) are
# deliberately excluded -- see docs/adr/0022 and features_spec.yaml.
FEATURE_COLUMNS = [
    "gold_adv_at_T", "xp_adv_at_T", "gold_adv_slope_5", "xp_adv_slope_5",
    "kills_diff_at_T", "deaths_in_last_5min_diff",
    "towers_killed_diff", "barracks_killed_diff", "roshan_killed_diff",
    "hero_pick_winrate_diff", "avg_rank_tier", "minute_T",
]
# first_blood_radiant (LIVE_ALLOWED) is boolean/None -- encoded separately, same as train.py.
# patch (PUBLIC_PREFLIGHT) is not used as a model input here -- it is a
# join key for hero_pick_winrate_diff and a segmentation dimension, not a
# numeric feature (same treatment as train.py, which also excludes it
# from FEATURE_COLUMNS for the same reason: it has no meaningful numeric
# ordering for a linear/GBM model).


def _prepare_xy(df: pd.DataFrame) -> tuple[pd.DataFrame, np.ndarray]:
    x = df[FEATURE_COLUMNS].copy()
    for column in x.columns:
        x[column] = pd.to_numeric(x[column], errors="coerce")
    x["first_blood_radiant"] = df["first_blood_radiant"].map({True: 1.0, False: 0.0}).fillna(0.5)
    x = x.fillna(0.0)
    y = df["radiant_win"].astype(int).values
    return x, y


def _git_commit() -> str | None:
    try:
        return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=Path(__file__).resolve().parents[3]).decode().strip()
    except Exception:
        return None


def train_and_evaluate(dataset_dir: Path) -> dict:
    train_df = pd.read_parquet(dataset_dir / "train.parquet")
    val_df = pd.read_parquet(dataset_dir / "val.parquet")
    calib_df = pd.read_parquet(dataset_dir / "calib.parquet")
    test_df = pd.read_parquet(dataset_dir / "test.parquet")
    dataset_metadata = json.loads((dataset_dir / "dataset.json").read_text(encoding="utf-8"))

    x_train, y_train = _prepare_xy(train_df)
    x_val, y_val = _prepare_xy(val_df)
    x_calib, y_calib = _prepare_xy(calib_df)
    x_test, y_test = _prepare_xy(test_df)

    scaler = StandardScaler()
    x_train_scaled = scaler.fit_transform(x_train)
    x_val_scaled = scaler.transform(x_val)
    x_calib_scaled = scaler.transform(x_calib)
    x_test_scaled = scaler.transform(x_test)

    # --- Baseline 1: constant 0.5 ---
    baseline_constant_proba = np.full_like(y_val, 0.5, dtype=float)
    baseline_constant_metrics = core_metrics(y_val, baseline_constant_proba)
    log.info("baseline (constant 0.5) on val: %s", baseline_constant_metrics)

    # --- Baseline 2: single-feature LogReg on gold_adv_at_T ---
    single_feature_model = LogisticRegression(random_state=RANDOM_SEED)
    single_feature_model.fit(x_train[[ABLATION_FEATURE]], y_train)
    baseline_single_feature_proba = single_feature_model.predict_proba(x_val[[ABLATION_FEATURE]])[:, 1]
    baseline_single_feature_metrics = core_metrics(y_val, baseline_single_feature_proba)
    log.info("baseline (single-feature LogReg on %s) on val: %s", ABLATION_FEATURE, baseline_single_feature_metrics)

    # --- Model 1: full Logistic Regression, with C selected on val ---
    # The same collinearity risk train.py's docs/adr/0018 found
    # (gold_adv_at_T / xp_adv_at_T / their _slope_5 variants) is still
    # present in this smaller feature set -- do not assume the prior C
    # selection transfers; re-run the grid search fresh.
    candidate_c_values = [1.0, 0.1, 0.01, 0.001, 1e-4, 1e-5, 1e-6]
    best_c, best_c_auc = None, -1.0
    c_search_results = {}
    for c in candidate_c_values:
        candidate = LogisticRegression(max_iter=3000, random_state=RANDOM_SEED, C=c)
        candidate.fit(x_train_scaled, y_train)
        candidate_auc = core_metrics(y_val, candidate.predict_proba(x_val_scaled)[:, 1])["auc_roc"]
        c_search_results[c] = candidate_auc
        if candidate_auc > best_c_auc:
            best_c, best_c_auc = c, candidate_auc
    log.info("LogisticRegression C search on val AUC: %s -- selected C=%s", c_search_results, best_c)

    logreg = LogisticRegression(max_iter=3000, random_state=RANDOM_SEED, C=best_c)
    logreg.fit(x_train_scaled, y_train)
    logreg_val_proba = logreg.predict_proba(x_val_scaled)[:, 1]
    logreg_val_metrics = core_metrics(y_val, logreg_val_proba)
    log.info("LogisticRegression on val: %s", logreg_val_metrics)

    coefficients = dict(zip(x_train.columns, logreg.coef_[0].tolist()))
    log.info("LogisticRegression coefficients: %s", coefficients)
    gold_adv_coef = coefficients.get(ABLATION_FEATURE)
    if gold_adv_coef is not None and gold_adv_coef <= 0:
        log.warning("gold_adv_at_T coefficient is non-positive (%.4f) -- expected positive per Dota strategy; investigate before shipping", gold_adv_coef)

    # --- Model 2: LightGBM ---
    lgbm_params = {
        "objective": "binary",
        "n_estimators": 200,
        "max_depth": 5,
        "learning_rate": 0.05,
        "num_leaves": 31,
        "random_state": RANDOM_SEED,
        "verbosity": -1,
    }
    lgbm = lgb.LGBMClassifier(**lgbm_params)
    lgbm.fit(x_train, y_train)
    lgbm_val_proba = lgbm.predict_proba(x_val)[:, 1]
    lgbm_val_metrics = core_metrics(y_val, lgbm_val_proba)
    log.info("LightGBM on val: %s", lgbm_val_metrics)

    auc_gain = lgbm_val_metrics["auc_roc"] - logreg_val_metrics["auc_roc"]
    chosen_model_name = "lightgbm" if auc_gain >= 0.01 else "logistic_regression"
    log.info("LightGBM AUC gain over LogisticRegression: %.4f -- chosen model: %s", auc_gain, chosen_model_name)

    if chosen_model_name == "lightgbm":
        chosen_model = lgbm
        raw_proba_calib = lgbm.predict_proba(x_calib)[:, 1]
        raw_proba_val = lgbm_val_proba
        predict_for_sanity = lambda row_dict: float(lgbm.predict_proba(pd.DataFrame([row_dict])[x_train.columns])[:, 1][0])  # noqa: E731
    else:
        chosen_model = logreg
        raw_proba_calib = logreg.predict_proba(x_calib_scaled)[:, 1]
        raw_proba_val = logreg_val_proba
        predict_for_sanity = lambda row_dict: float(  # noqa: E731
            logreg.predict_proba(scaler.transform(pd.DataFrame([row_dict])[x_train.columns]))[:, 1][0]
        )

    # --- Calibration: fit on CALIB, check on VAL ---
    pre_calibration_curve = calibration_curve(y_val, raw_proba_val, CALIBRATION_BINS)
    pre_calibration_ece = expected_calibration_error(pre_calibration_curve, len(y_val))
    log.info("pre-calibration ECE on val: %.4f", pre_calibration_ece)

    calibrator = fit_calibrator(raw_proba_calib, y_calib)
    calibrated_val_proba = apply_calibrator(calibrator, raw_proba_val)
    post_calibration_curve = calibration_curve(y_val, calibrated_val_proba, CALIBRATION_BINS)
    post_calibration_ece = expected_calibration_error(post_calibration_curve, len(y_val))
    log.info("post-calibration ECE on val: %.4f", post_calibration_ece)

    if pre_calibration_ece <= ECE_ISSUE_THRESHOLD:
        log.info("pre-calibration ECE already below threshold (%.4f <= %.4f) -- calibration applied anyway for consistency", pre_calibration_ece, ECE_ISSUE_THRESHOLD)

    # --- Touch test exactly once, here ---
    if chosen_model_name == "lightgbm":
        raw_proba_test = chosen_model.predict_proba(x_test)[:, 1]
    else:
        raw_proba_test = chosen_model.predict_proba(x_test_scaled)[:, 1]
    calibrated_test_proba = apply_calibrator(calibrator, raw_proba_test)
    test_metrics = core_metrics(y_test, calibrated_test_proba)
    log.info("FINAL test metrics (touched once): %s", test_metrics)

    segments = {
        "by_timestamp_bucket": segment_by_timestamp_bucket(test_df, y_test, calibrated_test_proba),
        "by_rank_tier": segment_by_rank_tier(test_df, y_test, calibrated_test_proba),
        "by_patch": segment_by_patch(test_df, y_test, calibrated_test_proba),
        "by_hero": segment_by_hero(test_df, y_test, calibrated_test_proba),
    }

    sanity_base_overrides = {
        "kills_diff_at_T": 0, "deaths_in_last_5min_diff": 0,
        "towers_killed_diff": 0, "barracks_killed_diff": 0, "roshan_killed_diff": 0,
        "hero_pick_winrate_diff": 0, "avg_rank_tier": 50, "minute_T": 15,
        "first_blood_radiant": 0.5,
    }

    def predict_for_sanity_live(row_overrides: dict) -> float:
        base = {c: 0 for c in x_train.columns}
        base.update(sanity_base_overrides)
        base.update(row_overrides)
        return predict_for_sanity(base)

    sanity = domain_sanity_checks(predict_for_sanity_live)

    evaluation = {
        "baselines": {
            "constant_0.5": baseline_constant_metrics,
            f"single_feature_logreg_{ABLATION_FEATURE}": baseline_single_feature_metrics,
        },
        "models": {
            "logistic_regression": logreg_val_metrics,
            "lightgbm": lgbm_val_metrics,
        },
        "chosen_model": chosen_model_name,
        "auc_gain_lightgbm_over_logreg": auc_gain,
        "logreg_coefficients": coefficients,
        "calibration": {
            "pre_calibration_ece_val": pre_calibration_ece,
            "post_calibration_ece_val": post_calibration_ece,
            "pre_calibration_curve_val": pre_calibration_curve,
            "post_calibration_curve_val": post_calibration_curve,
        },
        "test_metrics_touched_once": test_metrics,
        "segments": segments,
        "domain_sanity_checks": sanity,
        "feature_set": "live_allowed",
        "feature_columns": list(x_train.columns),
        "research_model_comparison_note": "See docs/adr/0021 for the research (full-feature, POSTGAME_ONLY-inclusive) model's test AUC 0.819 on the same dataset/split; this live-restricted variant is expected to score lower since several dropped features (gold_gini_radiant, gold_gini_dire, max_hero_gold_diff, teamfights_won_diff) carried real signal.",
    }

    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    model_id = f"win-probability-live-v1.0-{run_id}"
    model_dir = MODELS_DIR / model_id
    model_dir.mkdir(parents=True, exist_ok=True)

    import pickle

    with open(model_dir / "model.pkl", "wb") as f:
        pickle.dump({"model": chosen_model, "scaler": scaler if chosen_model_name == "logistic_regression" else None, "feature_columns": list(x_train.columns)}, f)
    with open(model_dir / "calibrator.pkl", "wb") as f:
        pickle.dump(calibrator, f)

    metadata = {
        "model_id": model_id,
        "model_name": "win-probability-live",
        "version": {"major": 1, "minor": 0},
        "training_run_id": run_id,
        "data": {
            "dataset_id": dataset_metadata["dataset_id"],
            "features_version": dataset_metadata["features_version"],
            "filters": {
                "lobby_type": 7,
                "game_mode": 22,
                "game_mode_note": "22=All Draft (Ranked); 23=Turbo. Verified via /constants/game_mode -- see docs/adr/0017.",
            },
            "record_counts": dataset_metadata["record_counts"],
            "split_cutoffs": dataset_metadata["splits"],
        },
        "code": {"git_commit": _git_commit()},
        "hyperparameters": {
            "chosen_model": chosen_model_name,
            "lightgbm": lgbm_params,
            "logistic_regression": {"max_iter": 3000, "random_state": RANDOM_SEED, "C": best_c, "C_search_candidates": candidate_c_values, "C_search_val_auc": c_search_results},
        },
        "seeds": {"random_seed": RANDOM_SEED},
        "libraries": {
            "python": sys.version.split()[0],
            "lightgbm": lgb.__version__,
            "scikit-learn": sklearn.__version__,
            "pandas": pd.__version__,
            "numpy": np.__version__,
        },
        "fairplay": {
            "contains_postgame_only_features": False,
            "feature_set": "live_allowed",
            "excluded_postgame_only_features": [
                "lh_diff_at_T", "gold_gini_radiant", "gold_gini_dire",
                "max_hero_gold_diff", "teamfights_won_diff", "key_item_count_diff",
            ],
            "note": "LIVE_ALLOWED/PUBLIC_PREFLIGHT-only feature set, verified against features_spec.yaml. Candidate for Ml.Inference wiring after ONNX export and validation. See docs/adr/0022.",
        },
        "evaluation_ref": "./evaluation.json",
    }

    (model_dir / "metadata.json").write_text(json.dumps(metadata, indent=2, default=str), encoding="utf-8")
    (model_dir / "evaluation.json").write_text(json.dumps(evaluation, indent=2, default=str), encoding="utf-8")

    log.info("model saved: %s", model_dir)
    return {"model_id": model_id, "metadata": metadata, "evaluation": evaluation}


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    if len(sys.argv) < 2:
        log.error("usage: python -m dota_training.models.train_live <dataset_dir>")
        sys.exit(1)
    train_and_evaluate(Path(sys.argv[1]))
