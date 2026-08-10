"""Draft baseline: P(radiant_win | 10 hero picks), per dota-ml's baseline
list (hero global winrate + matchup matrix + logistic regression). Reuses
the same train-only, patch-pinned discipline as
dota_training.stages.dataset's hero_pick_winrate_diff (see
tests/test_patch_pinning.py and docs/adr/0017/0018), extended here with a
pairwise hero-vs-hero matchup matrix as the second baseline signal.

Two models, matching dota-ml's mandated progression:
1. Hero global winrate diff alone (single feature -- the simplest possible baseline).
2. Logistic Regression over [hero global winrate diff, matchup advantage].
No LightGBM here: with only 2-3 engineered features (not 18 raw ones),
there is no meaningful non-linearity for a tree ensemble to capture, and
dota-ml's own guidance is to not reach for a bigger model without a
concrete reason to expect it helps.
"""

from __future__ import annotations

import itertools
import json
import logging
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pandas as pd
import sklearn
from sklearn.linear_model import LogisticRegression
from sklearn.preprocessing import StandardScaler

from dota_training.config import MODELS_DIR, RANDOM_SEED
from dota_training.models.calibrate import apply_calibrator, fit_calibrator
from dota_training.models.evaluate import (
    calibration_curve,
    core_metrics,
    expected_calibration_error,
    segment_by_patch,
)

log = logging.getLogger(__name__)

CALIBRATION_BINS = 10
ECE_ISSUE_THRESHOLD = 0.05
MATCHUP_PRIOR_STRENGTH = 10.0  # Bayesian shrinkage pseudo-count for sparse hero-vs-hero pairs, per dota-ml's shrinkage guidance
HERO_WINRATE_PRIOR_STRENGTH = 30.0  # same shrinkage discipline for the per-hero table itself -- see docs/adr/0019


def compute_hero_global_winrate(train_df: pd.DataFrame, overall_winrate: float) -> dict[tuple[int, int], float]:
    """(hero_id, patch) -> Bayesian-shrunk win rate, train-only,
    patch-pinned. Same approach as
    dota_training.stages.dataset.compute_hero_patch_winrates, duplicated
    here rather than imported: the two callers (win-probability DNA
    feature vs. draft baseline) are independently evolvable and this keeps
    that explicit, matching this project's existing precedent
    (LiveRecommendationInputTests' walker vs. ProfileFairPlayTests' local
    copy in the C# side of this project).

    Shrunk toward the train-split overall win rate, per dota-ml's explicit
    shrinkage requirement -- an unshrunk empirical rate is exactly the
    "won 8/10 games, noisy and overconfident" failure mode the skill
    warns about. See docs/adr/0019: the first version of this baseline
    used the raw empirical rate and it dominated the model with spurious
    100%/0% hero winrates on samples as small as 11-70 games.
    """
    records = []
    for _, row in train_df.iterrows():
        for hero_id in row["radiant_hero_ids"]:
            records.append({"hero_id": hero_id, "patch": row["patch"], "won": bool(row["radiant_win"])})
        for hero_id in row["dire_hero_ids"]:
            records.append({"hero_id": hero_id, "patch": row["patch"], "won": not bool(row["radiant_win"])})
    if not records:
        return {}
    hero_df = pd.DataFrame(records)
    grouped = hero_df.groupby(["hero_id", "patch"])["won"].agg(["sum", "count"])
    k = HERO_WINRATE_PRIOR_STRENGTH
    shrunk = (grouped["sum"] + overall_winrate * k) / (grouped["count"] + k)
    return {(int(h), int(p)): float(r) for (h, p), r in shrunk.items()}


def compute_matchup_matrix(train_df: pd.DataFrame, overall_winrate: float) -> dict[tuple[int, int], float]:
    """(hero_a, hero_b) with hero_a < hero_b -> hero_a's win rate when
    facing hero_b as an enemy (symmetric key, direction resolved at lookup
    time), Bayesian-shrunk toward the overall (train-only) win rate per
    dota-ml's shrinkage guidance -- most specific hero-vs-hero pairs have
    very few observations, and an unshrunk empirical rate would be noisy
    and overconfident (dota-ml's own 8/10-game example)."""
    wins: dict[tuple[int, int], int] = {}
    totals: dict[tuple[int, int], int] = {}

    for _, row in train_df.iterrows():
        radiant_won = bool(row["radiant_win"])
        for r_hero, d_hero in itertools.product(row["radiant_hero_ids"], row["dire_hero_ids"]):
            key = (min(r_hero, d_hero), max(r_hero, d_hero))
            totals[key] = totals.get(key, 0) + 1
            # "wins" counts wins for the SMALLER hero_id in the pair, facing the larger.
            smaller_is_radiant = r_hero < d_hero
            smaller_won = radiant_won if smaller_is_radiant else not radiant_won
            wins[key] = wins.get(key, 0) + (1 if smaller_won else 0)

    shrunk = {}
    k = MATCHUP_PRIOR_STRENGTH
    for key, total in totals.items():
        w = wins.get(key, 0)
        shrunk[key] = (w + overall_winrate * k) / (total + k)
    return shrunk


def _matchup_rate_smaller_hero(hero_a: int, hero_b: int, matrix: dict[tuple[int, int], float], overall_winrate: float) -> float:
    """Win rate of hero_a facing hero_b as an enemy, resolving the symmetric matrix key."""
    key = (min(hero_a, hero_b), max(hero_a, hero_b))
    rate_for_smaller = matrix.get(key, overall_winrate)
    return rate_for_smaller if hero_a < hero_b else (1.0 - rate_for_smaller)


def compute_draft_features(
    df: pd.DataFrame,
    hero_winrates: dict[tuple[int, int], float],
    matchup_matrix: dict[tuple[int, int], float],
    overall_winrate: float,
) -> pd.DataFrame:
    rows = []
    for _, row in df.iterrows():
        radiant_heroes = row["radiant_hero_ids"]
        dire_heroes = row["dire_hero_ids"]
        patch = row["patch"]

        radiant_wr = [hero_winrates.get((h, patch), overall_winrate) for h in radiant_heroes]
        dire_wr = [hero_winrates.get((h, patch), overall_winrate) for h in dire_heroes]
        hero_winrate_diff = (sum(radiant_wr) / len(radiant_wr)) - (sum(dire_wr) / len(dire_wr))

        matchup_rates = [
            _matchup_rate_smaller_hero(r, d, matchup_matrix, overall_winrate)
            for r, d in itertools.product(radiant_heroes, dire_heroes)
        ]
        matchup_advantage = (sum(matchup_rates) / len(matchup_rates)) - overall_winrate

        rows.append(
            {
                "match_id": row["match_id"],
                "hero_winrate_diff": hero_winrate_diff,
                "matchup_advantage": matchup_advantage,
                "radiant_win": row["radiant_win"],
                "patch": patch,
            }
        )
    return pd.DataFrame(rows)


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

    overall_winrate = float(train_df["radiant_win"].astype(int).mean())
    log.info("train-split overall Radiant win rate (prior): %.4f", overall_winrate)

    hero_winrates = compute_hero_global_winrate(train_df, overall_winrate)
    matchup_matrix = compute_matchup_matrix(train_df, overall_winrate)
    log.info("hero_winrates: %d (hero, patch) entries; matchup_matrix: %d hero pairs", len(hero_winrates), len(matchup_matrix))

    train_features = compute_draft_features(train_df, hero_winrates, matchup_matrix, overall_winrate)
    val_features = compute_draft_features(val_df, hero_winrates, matchup_matrix, overall_winrate)
    calib_features = compute_draft_features(calib_df, hero_winrates, matchup_matrix, overall_winrate)
    test_features = compute_draft_features(test_df, hero_winrates, matchup_matrix, overall_winrate)

    y_train = train_features["radiant_win"].astype(int).values
    y_val = val_features["radiant_win"].astype(int).values
    y_calib = calib_features["radiant_win"].astype(int).values
    y_test = test_features["radiant_win"].astype(int).values

    # --- Baseline 1: constant (train-split prior) ---
    baseline_constant_proba = np.full_like(y_val, overall_winrate, dtype=float)
    baseline_constant_metrics = core_metrics(y_val, baseline_constant_proba)
    log.info("baseline (constant train-prior %.4f) on val: %s", overall_winrate, baseline_constant_metrics)

    # --- Baseline 2: hero_winrate_diff alone via single-feature LogReg ---
    single_feature_model = LogisticRegression(random_state=RANDOM_SEED)
    single_feature_model.fit(train_features[["hero_winrate_diff"]], y_train)
    baseline_single_proba = single_feature_model.predict_proba(val_features[["hero_winrate_diff"]])[:, 1]
    baseline_single_metrics = core_metrics(y_val, baseline_single_proba)
    log.info("baseline (single-feature LogReg on hero_winrate_diff) on val: %s", baseline_single_metrics)

    # --- Model: Logistic Regression on [hero_winrate_diff, matchup_advantage] ---
    feature_cols = ["hero_winrate_diff", "matchup_advantage"]
    scaler = StandardScaler()
    x_train_scaled = scaler.fit_transform(train_features[feature_cols])
    x_val_scaled = scaler.transform(val_features[feature_cols])
    x_calib_scaled = scaler.transform(calib_features[feature_cols])
    x_test_scaled = scaler.transform(test_features[feature_cols])

    candidate_c_values = [1.0, 0.1, 0.01, 0.001]
    best_c, best_c_auc, c_search_results = None, -1.0, {}
    for c in candidate_c_values:
        candidate = LogisticRegression(max_iter=2000, random_state=RANDOM_SEED, C=c)
        candidate.fit(x_train_scaled, y_train)
        auc = core_metrics(y_val, candidate.predict_proba(x_val_scaled)[:, 1])["auc_roc"]
        c_search_results[c] = auc
        if auc > best_c_auc:
            best_c, best_c_auc = c, auc
    log.info("LogisticRegression C search on val AUC: %s -- selected C=%s", c_search_results, best_c)

    logreg = LogisticRegression(max_iter=2000, random_state=RANDOM_SEED, C=best_c)
    logreg.fit(x_train_scaled, y_train)
    logreg_val_proba = logreg.predict_proba(x_val_scaled)[:, 1]
    logreg_val_metrics = core_metrics(y_val, logreg_val_proba)
    log.info("LogisticRegression on val: %s", logreg_val_metrics)

    coefficients = dict(zip(feature_cols, logreg.coef_[0].tolist()))
    log.info("LogisticRegression coefficients: %s", coefficients)
    for name in feature_cols:
        if coefficients[name] <= 0:
            log.warning("%s coefficient is non-positive (%.4f) -- expected positive; investigate", name, coefficients[name])

    # --- Calibration: fit on CALIB, check on VAL ---
    raw_proba_calib = logreg.predict_proba(x_calib_scaled)[:, 1]
    pre_curve = calibration_curve(y_val, logreg_val_proba, CALIBRATION_BINS)
    pre_ece = expected_calibration_error(pre_curve, len(y_val))

    calibrator = fit_calibrator(raw_proba_calib, y_calib)
    calibrated_val_proba = apply_calibrator(calibrator, logreg_val_proba)
    post_curve = calibration_curve(y_val, calibrated_val_proba, CALIBRATION_BINS)
    post_ece = expected_calibration_error(post_curve, len(y_val))
    log.info("pre-calibration ECE on val: %.4f, post: %.4f", pre_ece, post_ece)

    # --- Touch test exactly once ---
    raw_proba_test = logreg.predict_proba(x_test_scaled)[:, 1]
    calibrated_test_proba = apply_calibrator(calibrator, raw_proba_test)
    test_metrics = core_metrics(y_test, calibrated_test_proba)
    log.info("FINAL test metrics (touched once): %s", test_metrics)

    # --- By-patch segmentation (see docs/adr/0021): the whole point of the
    # multi-patch rebuild is to actually measure this instead of assuming
    # a single patch, as the original 1863-match dataset forced.
    by_patch = segment_by_patch(test_features, y_test, calibrated_test_proba)
    log.info("by-patch segmentation (test): %s", by_patch)

    # --- Domain sanity check: a top-tier hero pool should not score below a random one ---
    top_heroes_by_winrate = sorted(
        {h for h, p in hero_winrates.keys()}, key=lambda h: hero_winrates.get((h, train_df["patch"].mode()[0]), overall_winrate), reverse=True
    )[:5]
    bottom_heroes_by_winrate = sorted(
        {h for h, p in hero_winrates.keys()}, key=lambda h: hero_winrates.get((h, train_df["patch"].mode()[0]), overall_winrate)
    )[:5]
    strong_pick_row = pd.DataFrame(
        [{"match_id": -1, "radiant_hero_ids": top_heroes_by_winrate, "dire_hero_ids": bottom_heroes_by_winrate, "patch": train_df["patch"].mode()[0], "radiant_win": True}]
    )
    weak_pick_row = pd.DataFrame(
        [{"match_id": -2, "radiant_hero_ids": bottom_heroes_by_winrate, "dire_hero_ids": top_heroes_by_winrate, "patch": train_df["patch"].mode()[0], "radiant_win": True}]
    )
    strong_features = compute_draft_features(strong_pick_row, hero_winrates, matchup_matrix, overall_winrate)
    weak_features = compute_draft_features(weak_pick_row, hero_winrates, matchup_matrix, overall_winrate)
    p_strong = float(logreg.predict_proba(scaler.transform(strong_features[feature_cols]))[:, 1][0])
    p_weak = float(logreg.predict_proba(scaler.transform(weak_features[feature_cols]))[:, 1][0])
    sanity = {
        "top5_vs_bottom5_heroes": {
            "passed": p_strong > p_weak,
            "p_radiant_wins_with_top5": p_strong,
            "p_radiant_wins_with_bottom5": p_weak,
        }
    }
    log.info("domain sanity check (top-5 vs bottom-5 hero pool): %s", sanity)

    evaluation = {
        "baselines": {
            "constant_train_prior": baseline_constant_metrics,
            "single_feature_logreg_hero_winrate_diff": baseline_single_metrics,
        },
        "model": {"logistic_regression": logreg_val_metrics},
        "logreg_coefficients": coefficients,
        "c_search_results": c_search_results,
        "calibration": {
            "pre_calibration_ece_val": pre_ece,
            "post_calibration_ece_val": post_ece,
            "pre_calibration_curve_val": pre_curve,
            "post_calibration_curve_val": post_curve,
        },
        "test_metrics_touched_once": test_metrics,
        "segments": {"by_patch": by_patch},
        "domain_sanity_checks": sanity,
        "overall_train_winrate_prior": overall_winrate,
        "hero_winrate_table_size": len(hero_winrates),
        "matchup_matrix_size": len(matchup_matrix),
    }

    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    model_id = f"draft-baseline-v1.0-{run_id}"
    model_dir = MODELS_DIR / model_id
    model_dir.mkdir(parents=True, exist_ok=True)

    import pickle

    with open(model_dir / "model.pkl", "wb") as f:
        pickle.dump(
            {
                "model": logreg,
                "scaler": scaler,
                "feature_columns": feature_cols,
                "hero_winrates": hero_winrates,
                "matchup_matrix": matchup_matrix,
                "overall_winrate": overall_winrate,
            },
            f,
        )
    with open(model_dir / "calibrator.pkl", "wb") as f:
        pickle.dump(calibrator, f)

    metadata = {
        "model_id": model_id,
        "model_name": "draft-baseline",
        "version": {"major": 1, "minor": 0},
        "training_run_id": run_id,
        "data": {
            "dataset_id": dataset_metadata["dataset_id"],
            "features_version": dataset_metadata["features_version"],
            "record_counts": dataset_metadata["record_counts"],
            "split_cutoffs": dataset_metadata["splits"],
        },
        "code": {"git_commit": _git_commit()},
        "hyperparameters": {
            "logistic_regression": {"max_iter": 2000, "random_state": RANDOM_SEED, "C": best_c, "C_search_candidates": candidate_c_values, "C_search_val_auc": c_search_results},
            "matchup_prior_strength": MATCHUP_PRIOR_STRENGTH,
        },
        "seeds": {"random_seed": RANDOM_SEED},
        "libraries": {
            "python": sys.version.split()[0],
            "scikit-learn": sklearn.__version__,
            "pandas": pd.__version__,
            "numpy": np.__version__,
        },
        "fairplay": {
            "contains_postgame_only_features": False,
            "note": "Hero picks, patch, and match outcome are all PublicPreflight/LiveAllowed-eligible data (draft is visible to both teams before the match starts). Not wired into the live app in this pass.",
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
        log.error("usage: python -m dota_training.models.draft_baseline <dataset_dir>")
        sys.exit(1)
    train_and_evaluate(Path(sys.argv[1]))
