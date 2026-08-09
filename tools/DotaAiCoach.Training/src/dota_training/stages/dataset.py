"""DATASET stage: FEATURES -> a versioned 4-way temporal split.

train / val / calib / test, strictly ordered by match_id (a monotonic
proxy for time, verified live -- see docs/adr/0017), split BY MATCH so a
single match's several (match, T) rows never straddle a split boundary.
"""

from __future__ import annotations

import json
import logging
from datetime import datetime, timezone

import pandas as pd
import pyarrow as pa
import pyarrow.parquet as pq

from dota_training.config import DATASETS_DIR, FEATURES_PATH, SPLIT_SHARES

log = logging.getLogger(__name__)


def compute_hero_patch_winrates(train_df: pd.DataFrame) -> dict[tuple[int, int], float]:
    """(hero_id, patch) -> win rate, computed from the train split ONLY.
    Patch-pinned: a match's own patch, never 'the latest patch' -- per
    dota-data's patch-pinning rule and leakage pattern 3. Returns a dict
    keyed by (hero_id, patch) so a later join never has to guess which
    patch applies."""
    one_row_per_match = train_df.drop_duplicates(subset="match_id")[["match_id", "patch", "radiant_win", "radiant_hero_ids", "dire_hero_ids"]]

    records = []
    for _, row in one_row_per_match.iterrows():
        for hero_id in row["radiant_hero_ids"]:
            records.append({"hero_id": hero_id, "patch": row["patch"], "won": bool(row["radiant_win"])})
        for hero_id in row["dire_hero_ids"]:
            records.append({"hero_id": hero_id, "patch": row["patch"], "won": not bool(row["radiant_win"])})

    if not records:
        return {}

    hero_df = pd.DataFrame(records)
    grouped = hero_df.groupby(["hero_id", "patch"])["won"].mean()
    return {(int(hero_id), int(patch)): float(rate) for (hero_id, patch), rate in grouped.items()}


def _side_winrate(hero_ids: list[int], patch: int, winrates: dict[tuple[int, int], float]) -> float:
    values = [winrates.get((h, patch), 0.5) for h in hero_ids]
    return sum(values) / len(values) if values else 0.5


def apply_hero_winrate_feature(df: pd.DataFrame, winrates: dict[tuple[int, int], float]) -> pd.DataFrame:
    df = df.copy()
    df["hero_pick_winrate_diff"] = df.apply(
        lambda row: _side_winrate(row["radiant_hero_ids"], row["patch"], winrates)
        - _side_winrate(row["dire_hero_ids"], row["patch"], winrates),
        axis=1,
    )
    return df


def build_dataset(features_path=FEATURES_PATH, datasets_dir=DATASETS_DIR, dataset_id: str | None = None) -> dict:
    df = pd.read_parquet(features_path)

    unique_matches = df[["match_id", "start_time"]].drop_duplicates().sort_values("match_id")
    n_matches = len(unique_matches)

    train_end = int(n_matches * SPLIT_SHARES["train"])
    val_end = train_end + int(n_matches * SPLIT_SHARES["val"])
    calib_end = val_end + int(n_matches * SPLIT_SHARES["calib"])

    match_ids_ordered = unique_matches["match_id"].tolist()
    split_match_ids = {
        "train": set(match_ids_ordered[:train_end]),
        "val": set(match_ids_ordered[train_end:val_end]),
        "calib": set(match_ids_ordered[val_end:calib_end]),
        "test": set(match_ids_ordered[calib_end:]),
    }

    if dataset_id is None:
        dataset_id = f"ranked-ad-{n_matches}-{datetime.now(timezone.utc).strftime('%Y%m%d')}"

    output_dir = datasets_dir / dataset_id
    output_dir.mkdir(parents=True, exist_ok=True)

    # hero_pick_winrate_diff is computed from TRAIN ONLY, patch-pinned, then
    # applied to every split -- never recomputed per-split, which would let
    # val/calib/test rows influence their own feature values (a leakage
    # pattern 5 variant: information about the split itself leaking in).
    train_df = df[df["match_id"].isin(split_match_ids["train"])]
    hero_winrates = compute_hero_patch_winrates(train_df)
    df = apply_hero_winrate_feature(df, hero_winrates)

    record_counts = {}
    cutoffs = {}
    for split_name, ids in split_match_ids.items():
        split_df = df[df["match_id"].isin(ids)]
        pq.write_table(pa.Table.from_pandas(split_df, preserve_index=False), output_dir / f"{split_name}.parquet")
        record_counts[split_name] = len(split_df)

        split_starts = unique_matches[unique_matches["match_id"].isin(ids)]["start_time"]
        cutoffs[split_name] = {
            "min_start_time": int(split_starts.min()) if len(split_starts) else None,
            "max_start_time": int(split_starts.max()) if len(split_starts) else None,
            "match_count": len(ids),
        }

    metadata = {
        "dataset_id": dataset_id,
        "features_version": "1.0",
        "total_matches": n_matches,
        "split_shares_configured": SPLIT_SHARES,
        "splits": cutoffs,
        "record_counts": record_counts,
    }

    (output_dir / "dataset.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    log.info("DATASET %s: %s", dataset_id, record_counts)
    return metadata


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    build_dataset()
