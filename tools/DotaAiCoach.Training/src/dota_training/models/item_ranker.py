"""Item Recommendation Ranker baseline: ranks candidate items by
likelihood of being the next purchase, given (hero, minute, items owned).
See docs/adr/0020.

Per docs/MASTER_PLAN.md Module 11: P(next_item | current_state), with an
explicit warning against recommending "simply by winrate" (survivor
bias -- the Divine Rapier example). Baseline progression: frequency-
conditioned -> LightGBM ranker (LambdaMART). "Rules" and "nearest
historical states" tiers are not reimplemented here -- this project
already ships a hand-curated rule engine in
src/DotaAiCoach.RecommendationEngines/NextItemSelector.cs; porting it to
Python would duplicate domain curation rather than produce a new data
point about the ML approach.
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

from dota_training.config import MODELS_DIR, RANDOM_SEED

log = logging.getLogger(__name__)

HERO_ITEM_PRIOR_STRENGTH = 20.0
HERO_MINUTE_BUCKET_PRIOR_STRENGTH = 15.0
MIN_SEGMENT_ROWS = 30  # lower than the 100-row gate used elsewhere: fewer rows are available per (hero, item) cell at this dataset scale


def _minute_bucket(minute: int) -> str:
    if minute < 15:
        return "early"
    if minute < 30:
        return "mid"
    return "late"


def compute_item_popularity_tables(train_df: pd.DataFrame, all_items: list[str]) -> dict:
    """Train-split-only, Bayesian-shrunk popularity tables:
    global -> per-hero -> per-(hero, minute_bucket), each shrunk toward
    its own parent (nested shrinkage, same pattern as
    dota_training.models.draft_baseline's matchup matrix shrinking toward
    the overall win rate)."""
    global_counts = train_df["next_item"].value_counts()
    total = len(train_df)
    global_popularity = {item: global_counts.get(item, 0) / total for item in all_items}

    hero_popularity: dict[tuple[int, str], float] = {}
    for hero_id, group in train_df.groupby("hero_id"):
        hero_total = len(group)
        hero_counts = group["next_item"].value_counts()
        for item in all_items:
            prior = global_popularity[item]
            k = HERO_ITEM_PRIOR_STRENGTH
            shrunk = (hero_counts.get(item, 0) + prior * k) / (hero_total + k)
            hero_popularity[(hero_id, item)] = shrunk

    hero_minute_popularity: dict[tuple[int, str, str], float] = {}
    train_df = train_df.copy()
    train_df["minute_bucket"] = train_df["minute"].apply(_minute_bucket)
    for (hero_id, bucket), group in train_df.groupby(["hero_id", "minute_bucket"]):
        bucket_total = len(group)
        bucket_counts = group["next_item"].value_counts()
        for item in all_items:
            prior = hero_popularity.get((hero_id, item), global_popularity[item])
            k = HERO_MINUTE_BUCKET_PRIOR_STRENGTH
            shrunk = (bucket_counts.get(item, 0) + prior * k) / (bucket_total + k)
            hero_minute_popularity[(hero_id, bucket, item)] = shrunk

    return {
        "global": global_popularity,
        "hero": hero_popularity,
        "hero_minute": hero_minute_popularity,
    }


def build_candidate_rows(df: pd.DataFrame, all_items: list[str], tables: dict) -> pd.DataFrame:
    """One row per (state, candidate_item), candidate pool = all_items minus items_owned_before.
    Grouped by the original row's position (query_id) for ranking.
    Vectorized: a naive per-row Python loop over ~140k rows x ~114 items
    (~16M candidate rows across all splits) was impractically slow."""
    df = df.reset_index(drop=True)
    df = df.assign(query_id=df.index, minute_bucket=df["minute"].apply(_minute_bucket))

    all_items_df = pd.DataFrame({"candidate_item": all_items})
    # Cross join: every state row x every candidate item.
    expanded = df.merge(all_items_df, how="cross")

    # Membership test via a per-query bitmask-free approach: explode
    # items_owned_before into (query_id, item) pairs and anti-join.
    owned_pairs = df[["query_id", "items_owned_before"]].explode("items_owned_before").dropna()
    owned_pairs = owned_pairs.rename(columns={"items_owned_before": "candidate_item"})
    owned_pairs["is_candidate_already_owned"] = True

    expanded = expanded.merge(owned_pairs, on=["query_id", "candidate_item"], how="left")
    # dtype must be a real bool, not object -- object-dtype ~True/~False
    # silently produce -2/-1 (Python int bitwise NOT) instead of a boolean
    # mask, which corrupts the filter below without raising.
    expanded["is_candidate_already_owned"] = expanded["is_candidate_already_owned"].fillna(False).astype(bool)
    expanded = expanded[~expanded["is_candidate_already_owned"]]

    expanded["item_global_popularity"] = expanded["candidate_item"].map(tables["global"])

    hero_pop_series = pd.Series(tables["hero"])
    hero_pop_series.index = pd.MultiIndex.from_tuples(hero_pop_series.index, names=["hero_id", "candidate_item"])
    expanded = expanded.set_index(["hero_id", "candidate_item"])
    expanded["item_popularity_for_hero"] = hero_pop_series.reindex(expanded.index)
    expanded["item_popularity_for_hero"] = expanded["item_popularity_for_hero"].fillna(expanded["item_global_popularity"])
    expanded = expanded.reset_index()

    hero_minute_pop_series = pd.Series(tables["hero_minute"])
    hero_minute_pop_series.index = pd.MultiIndex.from_tuples(hero_minute_pop_series.index, names=["hero_id", "minute_bucket", "candidate_item"])
    expanded = expanded.set_index(["hero_id", "minute_bucket", "candidate_item"])
    expanded["item_popularity_for_hero_minute_bucket"] = hero_minute_pop_series.reindex(expanded.index)
    expanded["item_popularity_for_hero_minute_bucket"] = expanded["item_popularity_for_hero_minute_bucket"].fillna(expanded["item_global_popularity"])
    expanded = expanded.reset_index()

    expanded["relevant"] = (expanded["candidate_item"] == expanded["next_item"]).astype(int)

    return expanded[
        [
            "query_id", "match_id", "player_slot", "hero_id", "minute", "n_items_owned_before",
            "candidate_item", "is_candidate_already_owned",
            "item_popularity_for_hero", "item_popularity_for_hero_minute_bucket", "item_global_popularity",
            "relevant",
        ]
    ]


def _ndcg_and_mrr(candidate_df: pd.DataFrame, scores: np.ndarray, k_values: tuple[int, ...] = (3, 5)) -> dict:
    """Vectorized NDCG@k / MRR for the single-relevant-document-per-query
    case (exactly one true next_item per row here). A per-group Python
    loop calling sklearn's ndcg_score once per (query, k) was measured to
    be impractically slow at this dataset's scale (~20k+ queries x 114
    candidates) -- this closed-form computation (relevant item's rank
    position determines both NDCG@k and reciprocal rank directly, no
    per-group sklearn call needed) is mathematically equivalent for
    binary, single-relevant-item relevance and orders of magnitude faster."""
    candidate_df = candidate_df.copy()
    candidate_df["score"] = scores

    # Rank within each query group by score descending (1 = top-ranked).
    candidate_df["rank"] = candidate_df.groupby("query_id")["score"].rank(method="first", ascending=False)

    relevant_rows = candidate_df[candidate_df["relevant"] == 1]
    ranks = relevant_rows["rank"].values  # one rank per query that has a relevant candidate

    reciprocal_ranks = 1.0 / ranks
    # Binary relevance, single relevant doc: DCG@k = 1/log2(rank+1) if rank<=k else 0.
    # IDCG@k = 1 (best case: relevant item ranked first) for every query
    # that has at least one candidate, so NDCG@k = DCG@k directly.
    ndcg_results = {}
    for k in k_values:
        within_k = ranks <= k
        dcg = np.where(within_k, 1.0 / np.log2(ranks + 1), 0.0)
        ndcg_results[k] = dcg

    return {
        **{f"ndcg@{k}": float(np.mean(v)) if len(v) else None for k, v in ndcg_results.items()},
        "mrr": float(np.mean(reciprocal_ranks)) if len(reciprocal_ranks) else None,
        "n_queries": int(candidate_df["query_id"].nunique()),
        "n_queries_with_relevant_candidate": int(len(ranks)),
    }


def evaluate_popularity_baseline(df: pd.DataFrame, all_items: list[str], tables: dict, table_key: str) -> dict:
    """Baseline evaluation: rank candidates purely by a popularity table (no model)."""
    candidate_df = build_candidate_rows(df, all_items, tables)
    if table_key == "global":
        scores = candidate_df["item_global_popularity"].values
    elif table_key == "hero":
        scores = candidate_df["item_popularity_for_hero"].values
    else:
        raise ValueError(table_key)
    return _ndcg_and_mrr(candidate_df, scores)


def train_and_evaluate(dataset_dir: Path) -> dict:
    train_df = pd.read_parquet(dataset_dir / "train.parquet")
    val_df = pd.read_parquet(dataset_dir / "val.parquet")
    test_df = pd.read_parquet(dataset_dir / "test.parquet")
    dataset_metadata = json.loads((dataset_dir / "dataset.json").read_text(encoding="utf-8"))

    all_items = sorted(train_df["next_item"].unique())
    log.info("train items: %d unique meaningful items", len(all_items))

    tables = compute_item_popularity_tables(train_df, all_items)

    # --- Baseline 2 (unconditional "rule"): global popularity ---
    baseline_global = evaluate_popularity_baseline(val_df, all_items, tables, "global")
    log.info("baseline (global popularity, unconditional 'rule') on val: %s", baseline_global)

    # --- Baseline 1 (frequency-conditioned): per-hero, Bayesian-shrunk ---
    baseline_hero = evaluate_popularity_baseline(val_df, all_items, tables, "hero")
    log.info("baseline (per-hero popularity, Bayesian-shrunk) on val: %s", baseline_hero)

    # --- Model: LightGBM LambdaMART ranker ---
    feature_cols = [
        "minute",
        "n_items_owned_before",
        "item_popularity_for_hero",
        "item_popularity_for_hero_minute_bucket",
        "item_global_popularity",
    ]

    train_candidates = build_candidate_rows(train_df, all_items, tables)
    val_candidates = build_candidate_rows(val_df, all_items, tables)
    test_candidates = build_candidate_rows(test_df, all_items, tables)

    assert not train_candidates["is_candidate_already_owned"].any(), "candidate pool must never include an already-owned item"
    assert not val_candidates["is_candidate_already_owned"].any(), "candidate pool must never include an already-owned item"

    train_candidates = train_candidates.sort_values("query_id")
    val_candidates = val_candidates.sort_values("query_id")
    test_candidates = test_candidates.sort_values("query_id")

    train_groups = train_candidates.groupby("query_id", sort=False).size().values
    val_groups = val_candidates.groupby("query_id", sort=False).size().values

    ranker_params = {
        "objective": "lambdarank",
        "n_estimators": 100,
        "num_leaves": 15,
        "learning_rate": 0.1,
        "min_child_samples": 20,
        "random_state": RANDOM_SEED,
        "verbosity": -1,
        "n_jobs": 4,
    }
    log.info("fitting LightGBM ranker on %d train candidate rows, %d val candidate rows...", len(train_candidates), len(val_candidates))
    fit_start = datetime.now(timezone.utc)
    ranker = lgb.LGBMRanker(**ranker_params)
    ranker.fit(
        train_candidates[feature_cols],
        train_candidates["relevant"],
        group=train_groups,
        eval_set=[(val_candidates[feature_cols], val_candidates["relevant"])],
        eval_group=[val_groups],
        eval_at=[3, 5],
    )
    log.info("fit completed in %s", datetime.now(timezone.utc) - fit_start)

    val_scores = ranker.predict(val_candidates[feature_cols])
    ranker_val_metrics = _ndcg_and_mrr(val_candidates, val_scores)
    log.info("LightGBM ranker on val: %s", ranker_val_metrics)

    # --- Touch test exactly once ---
    log.info("scoring test candidates (%d rows)...", len(test_candidates))
    test_scores = ranker.predict(test_candidates[feature_cols])
    log.info("computing test NDCG/MRR...")
    test_metrics = _ndcg_and_mrr(test_candidates, test_scores)
    log.info("FINAL test metrics (touched once): %s", test_metrics)

    # --- Segment by minute bucket ---
    # test_df's row order (reset to 0..n-1) is the same order query_id was
    # assigned in build_candidate_rows, so query_id == test_df's positional index.
    test_df_indexed = test_df.reset_index(drop=True)
    test_df_indexed["query_id"] = test_df_indexed.index
    test_df_indexed["minute_bucket"] = test_df_indexed["minute"].apply(_minute_bucket)

    segments_by_bucket = {}
    for bucket in ("early", "mid", "late"):
        bucket_query_ids = set(test_df_indexed[test_df_indexed["minute_bucket"] == bucket]["query_id"])
        bucket_candidates = test_candidates[test_candidates["query_id"].isin(bucket_query_ids)]
        if bucket_candidates["query_id"].nunique() < 5:
            segments_by_bucket[bucket] = {"n_queries": int(bucket_candidates["query_id"].nunique()), "underpowered": True}
            continue
        bucket_scores = ranker.predict(bucket_candidates[feature_cols])
        segments_by_bucket[bucket] = _ndcg_and_mrr(bucket_candidates, bucket_scores)
    log.info("segments by minute bucket: %s", segments_by_bucket)

    # --- Domain sanity check: ranker vs Baseline 1 top-1 agreement ---
    top1_ranker = test_candidates.assign(score=test_scores).sort_values(["query_id", "score"], ascending=[True, False]).groupby("query_id").first()
    top1_baseline = test_candidates.assign(score=test_candidates["item_popularity_for_hero"]).sort_values(
        ["query_id", "score"], ascending=[True, False]
    ).groupby("query_id").first()
    agreement_rate = float((top1_ranker["candidate_item"].values == top1_baseline["candidate_item"].values).mean())
    chance_rate = 1.0 / len(all_items)
    sanity = {
        "ranker_vs_baseline1_top1_agreement_rate": agreement_rate,
        "chance_rate": chance_rate,
        "passed": agreement_rate > chance_rate * 3,  # meaningfully above chance, not a strict correctness bar
    }
    log.info("domain sanity check (ranker vs baseline-1 top-1 agreement): %s", sanity)

    evaluation = {
        "baselines": {
            "global_popularity_unconditional_rule": baseline_global,
            "hero_popularity_bayesian_shrunk": baseline_hero,
        },
        "model": {"lightgbm_lambdamart": ranker_val_metrics},
        "test_metrics_touched_once": test_metrics,
        "segments_by_minute_bucket": segments_by_bucket,
        "domain_sanity_check": sanity,
        "n_unique_items": len(all_items),
        "candidate_pool_note": "v1.0 candidate pool = all meaningful items minus items_owned_before, NOT gold-affordability-filtered -- see docs/adr/0020 Consequences.",
    }

    def _git_commit() -> str | None:
        try:
            return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=Path(__file__).resolve().parents[3]).decode().strip()
        except Exception:
            return None

    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    model_id = f"item-ranker-v1.0-{run_id}"
    model_dir = MODELS_DIR / model_id
    model_dir.mkdir(parents=True, exist_ok=True)

    import pickle

    with open(model_dir / "model.pkl", "wb") as f:
        pickle.dump({"model": ranker, "feature_columns": feature_cols, "tables": tables, "all_items": all_items}, f)

    metadata = {
        "model_id": model_id,
        "model_name": "item-ranker",
        "version": {"major": 1, "minor": 0},
        "training_run_id": run_id,
        "data": {
            "dataset_id": dataset_metadata["dataset_id"],
            "record_counts": dataset_metadata["record_counts"],
            "match_counts": dataset_metadata["match_counts"],
        },
        "code": {"git_commit": _git_commit()},
        "hyperparameters": {
            "lightgbm_ranker": ranker_params,
            "hero_item_prior_strength": HERO_ITEM_PRIOR_STRENGTH,
            "hero_minute_bucket_prior_strength": HERO_MINUTE_BUCKET_PRIOR_STRENGTH,
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
            "note": "Hero, minute, items owned, and item popularity are all LiveAllowed/PublicPreflight-eligible. Not wired into the live app in this pass.",
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
        log.error("usage: python -m dota_training.models.item_ranker <dataset_dir>")
        sys.exit(1)
    train_and_evaluate(Path(sys.argv[1]))
