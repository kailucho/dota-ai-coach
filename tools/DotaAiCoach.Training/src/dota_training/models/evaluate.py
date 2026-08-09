"""Evaluation: AUC-ROC, log loss, Brier, calibration, segmented breakdowns,
domain sanity checks -- per dota-ml/references/evaluation.md and
validation-and-calibration.md."""

from __future__ import annotations

import numpy as np
import pandas as pd
from sklearn.metrics import brier_score_loss, log_loss, roc_auc_score

from dota_training.config import CALIBRATION_BINS, MIN_SEGMENT_ROWS


def core_metrics(y_true: np.ndarray, y_pred_proba: np.ndarray) -> dict:
    return {
        "auc_roc": float(roc_auc_score(y_true, y_pred_proba)),
        "log_loss": float(log_loss(y_true, y_pred_proba)),
        "brier": float(brier_score_loss(y_true, y_pred_proba)),
        "n": int(len(y_true)),
    }


def calibration_curve(y_true: np.ndarray, y_pred_proba: np.ndarray, n_bins: int = CALIBRATION_BINS) -> list[dict]:
    """Reliability curve: predicted-probability bucket vs. observed frequency, on held-out data only."""
    bins = np.linspace(0, 1, n_bins + 1)
    bin_indices = np.digitize(y_pred_proba, bins) - 1
    bin_indices = np.clip(bin_indices, 0, n_bins - 1)

    rows = []
    for b in range(n_bins):
        mask = bin_indices == b
        n = int(mask.sum())
        if n == 0:
            rows.append({"bin": b, "predicted_mean": None, "observed_rate": None, "n": 0})
            continue
        rows.append(
            {
                "bin": b,
                "predicted_mean": float(y_pred_proba[mask].mean()),
                "observed_rate": float(y_true[mask].mean()),
                "n": n,
            }
        )
    return rows


def expected_calibration_error(curve: list[dict], total_n: int) -> float:
    ece = 0.0
    for row in curve:
        if row["n"] == 0:
            continue
        ece += (row["n"] / total_n) * abs(row["predicted_mean"] - row["observed_rate"])
    return ece


def segment_by_timestamp_bucket(df: pd.DataFrame, y_true: np.ndarray, y_pred_proba: np.ndarray) -> dict:
    buckets = {
        "early_5_15": (5, 15),
        "mid_20_30": (20, 30),
        "late_35_plus": (35, 999),
    }
    result = {}
    for name, (lo, hi) in buckets.items():
        mask = (df["minute_T"] >= lo) & (df["minute_T"] <= hi)
        result[name] = _segment_result(y_true, y_pred_proba, mask.values)
    return result


def segment_by_rank_tier(df: pd.DataFrame, y_true: np.ndarray, y_pred_proba: np.ndarray) -> dict:
    def tier_bucket(avg_rank_tier):
        if pd.isna(avg_rank_tier):
            return "unknown"
        if avg_rank_tier < 30:
            return "herald_to_crusader"
        if avg_rank_tier < 60:
            return "archon_to_legend"
        return "ancient_plus"

    buckets = df["avg_rank_tier"].apply(tier_bucket)
    result = {}
    for name in buckets.unique():
        mask = (buckets == name).values
        result[name] = _segment_result(y_true, y_pred_proba, mask)
    return result


def segment_by_patch(df: pd.DataFrame, y_true: np.ndarray, y_pred_proba: np.ndarray) -> dict:
    result = {}
    for patch in df["patch"].dropna().unique():
        mask = (df["patch"] == patch).values
        result[str(patch)] = _segment_result(y_true, y_pred_proba, mask)
    return result


def segment_by_hero(df: pd.DataFrame, y_true: np.ndarray, y_pred_proba: np.ndarray) -> dict:
    """Only heroes appearing (radiant or dire) in >= MIN_SEGMENT_ROWS rows get a reported segment."""
    result = {}
    all_hero_ids = set()
    for heroes in df["radiant_hero_ids"]:
        all_hero_ids.update(heroes)
    for heroes in df["dire_hero_ids"]:
        all_hero_ids.update(heroes)

    for hero_id in sorted(all_hero_ids):
        mask = (df["radiant_hero_ids"].apply(lambda h: hero_id in h) | df["dire_hero_ids"].apply(lambda h: hero_id in h)).values
        if mask.sum() < MIN_SEGMENT_ROWS:
            continue
        result[str(hero_id)] = _segment_result(y_true, y_pred_proba, mask)

    return result


def _segment_result(y_true: np.ndarray, y_pred_proba: np.ndarray, mask: np.ndarray) -> dict:
    n = int(mask.sum())
    if n < MIN_SEGMENT_ROWS:
        return {"n": n, "underpowered": True, "note": f"fewer than {MIN_SEGMENT_ROWS} rows; metrics omitted as unreliable"}
    if len(set(y_true[mask])) < 2:
        return {"n": n, "underpowered": True, "note": "only one class present in this segment; AUC undefined"}
    return {"n": n, **core_metrics(y_true[mask], y_pred_proba[mask])}


def domain_sanity_checks(predict_fn) -> dict:
    """Hand-built scenarios with an obvious expected direction, per
    evaluation.md's exact example: 'more gold advantage at the same
    timestamp should not decrease predicted win probability, all else equal.'
    predict_fn takes a dict of feature overrides (rest zeroed/defaulted)
    and returns a scalar probability."""
    base = {
        "gold_adv_at_T": 0, "xp_adv_at_T": 0, "gold_adv_slope_5": 0, "xp_adv_slope_5": 0,
        "lh_diff_at_T": 0, "gold_gini_radiant": 0, "gold_gini_dire": 0, "max_hero_gold_diff": 0,
        "kills_diff_at_T": 0, "deaths_in_last_5min_diff": 0, "teamfights_won_diff": 0,
        "towers_killed_diff": 0, "barracks_killed_diff": 0, "roshan_killed_diff": 0,
        "key_item_count_diff": 0, "hero_pick_winrate_diff": 0, "avg_rank_tier": 50, "minute_T": 15,
        "first_blood_radiant": 0.5,
    }

    monotonic_probas = []
    for gold_adv in (-10000, -5000, 0, 5000, 10000):
        row = dict(base, gold_adv_at_T=gold_adv)
        monotonic_probas.append(predict_fn(row))
    is_monotonic = all(monotonic_probas[i] <= monotonic_probas[i + 1] + 1e-6 for i in range(len(monotonic_probas) - 1))

    p_at_zero = predict_fn(dict(base))
    near_half = abs(p_at_zero - 0.5) < 0.15

    negated = {k: (-v if isinstance(v, (int, float)) and k.endswith(("_diff", "_adv")) else v) for k, v in base.items()}
    p_positive = predict_fn(dict(base, gold_adv_at_T=5000, xp_adv_at_T=3000))
    p_negative = predict_fn(dict(negated, gold_adv_at_T=-5000, xp_adv_at_T=-3000))
    symmetric = abs((p_positive) - (1 - p_negative)) < 0.15

    return {
        "gold_advantage_monotonicity": {"passed": is_monotonic, "probabilities": monotonic_probas},
        "near_even_at_zero_diffs": {"passed": near_half, "predicted_proba": p_at_zero},
        "symmetry_check": {"passed": symmetric, "p_positive": p_positive, "p_negative": p_negative},
    }
