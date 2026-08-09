"""ITEM DATASET stage: assigns each item_events.parquet row to the same
4-way temporal match-level split used by the win-probability and draft
models (same match population, same match_id ordering) -- see
docs/adr/0017 (original split), docs/adr/0019 (draft model reuse),
docs/adr/0020 (this model)."""

from __future__ import annotations

import json
import logging
from datetime import datetime, timezone

import pandas as pd
import pyarrow as pa
import pyarrow.parquet as pq

from dota_training.config import DATA_DIR, DATASETS_DIR, SPLIT_SHARES

log = logging.getLogger(__name__)


def build_item_dataset(item_events_path=None, datasets_dir=DATASETS_DIR, dataset_id: str | None = None) -> dict:
    if item_events_path is None:
        item_events_path = DATA_DIR / "item_events.parquet"

    df = pd.read_parquet(item_events_path)

    unique_matches = sorted(df["match_id"].unique())
    n_matches = len(unique_matches)

    train_end = int(n_matches * SPLIT_SHARES["train"])
    val_end = train_end + int(n_matches * SPLIT_SHARES["val"])
    calib_end = val_end + int(n_matches * SPLIT_SHARES["calib"])

    split_match_ids = {
        "train": set(unique_matches[:train_end]),
        "val": set(unique_matches[train_end:val_end]),
        "calib": set(unique_matches[val_end:calib_end]),
        "test": set(unique_matches[calib_end:]),
    }

    if dataset_id is None:
        dataset_id = f"item-events-{n_matches}-{datetime.now(timezone.utc).strftime('%Y%m%d')}"

    output_dir = datasets_dir / dataset_id
    output_dir.mkdir(parents=True, exist_ok=True)

    record_counts = {}
    match_counts = {}
    for split_name, ids in split_match_ids.items():
        split_df = df[df["match_id"].isin(ids)]
        pq.write_table(pa.Table.from_pandas(split_df, preserve_index=False), output_dir / f"{split_name}.parquet")
        record_counts[split_name] = len(split_df)
        match_counts[split_name] = len(ids)

    metadata = {
        "dataset_id": dataset_id,
        "total_matches": n_matches,
        "split_shares_configured": SPLIT_SHARES,
        "record_counts": record_counts,
        "match_counts": match_counts,
    }
    (output_dir / "dataset.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    log.info("ITEM DATASET %s: %s", dataset_id, record_counts)
    return metadata


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    build_item_dataset()
