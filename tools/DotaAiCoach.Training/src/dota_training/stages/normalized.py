"""NORMALIZED stage: consistent IDs, non-null patch/timestamp, deduplicated
by match_id — per dota-data's pipeline-stages.md and data-quality-rules.md."""

from __future__ import annotations

import logging

import pyarrow.parquet as pq
import pyarrow as pa

from dota_training.config import BRONZE_PATH, NORMALIZED_PATH

log = logging.getLogger(__name__)


def build_normalized(bronze_path=BRONZE_PATH, output_path=NORMALIZED_PATH) -> int:
    table = pq.read_table(bronze_path)
    df = table.to_pandas()

    before = len(df)

    # Rule 3: patch and start_time must be non-null on every record.
    df = df[df["start_time"].notna() & df["patch"].notna()]
    log.info("NORMALIZED: dropped %d rows missing patch/start_time", before - len(df))

    # Rule 4: dedup by match_id. Tie-break: keep the most recently fetched
    # RAW envelope (a later fetch of the same match_id is never less
    # complete, since match detail for a finished match doesn't change,
    # but a later fetch reflects our own most recent, presumably
    # highest-fidelity capture attempt).
    before = len(df)
    df = df.sort_values("raw_fetched_at_utc").drop_duplicates(subset="match_id", keep="last")
    log.info("NORMALIZED: dropped %d duplicate match_id rows (kept most recently fetched)", before - len(df))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    pq.write_table(pa.Table.from_pandas(df, preserve_index=False), output_path)
    return len(df)


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    build_normalized()
