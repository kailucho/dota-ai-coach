"""DRAFT DATASET stage: one row per match (not per timestamp) for the
draft baseline, per dota-ml's baseline list: hero global winrate + matchup
matrix + logistic regression. Reuses the same match population as the win
probability model (FEATURES.parquet, deduped to one row per match) and the
same 4-way temporal split discipline as docs/adr/0017/0018.
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


def build_draft_dataset(features_path=FEATURES_PATH, datasets_dir=DATASETS_DIR, dataset_id: str | None = None) -> dict:
    df = pd.read_parquet(features_path)
    match_df = df.drop_duplicates(subset="match_id")[
        ["match_id", "radiant_hero_ids", "dire_hero_ids", "radiant_win", "patch", "start_time"]
    ].sort_values("match_id")

    n_matches = len(match_df)
    train_end = int(n_matches * SPLIT_SHARES["train"])
    val_end = train_end + int(n_matches * SPLIT_SHARES["val"])
    calib_end = val_end + int(n_matches * SPLIT_SHARES["calib"])

    match_ids_ordered = match_df["match_id"].tolist()
    split_match_ids = {
        "train": match_ids_ordered[:train_end],
        "val": match_ids_ordered[train_end:val_end],
        "calib": match_ids_ordered[val_end:calib_end],
        "test": match_ids_ordered[calib_end:],
    }

    if dataset_id is None:
        dataset_id = f"draft-ranked-ad-{n_matches}-{datetime.now(timezone.utc).strftime('%Y%m%d')}"

    output_dir = datasets_dir / dataset_id
    output_dir.mkdir(parents=True, exist_ok=True)

    record_counts = {}
    cutoffs = {}
    for split_name, ids in split_match_ids.items():
        split_df = match_df[match_df["match_id"].isin(ids)]
        pq.write_table(pa.Table.from_pandas(split_df, preserve_index=False), output_dir / f"{split_name}.parquet")
        record_counts[split_name] = len(split_df)
        cutoffs[split_name] = {
            "min_start_time": int(split_df["start_time"].min()) if len(split_df) else None,
            "max_start_time": int(split_df["start_time"].max()) if len(split_df) else None,
            "match_count": len(split_df),
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
    log.info("DRAFT DATASET %s: %s", dataset_id, record_counts)
    return metadata


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    build_draft_dataset()
