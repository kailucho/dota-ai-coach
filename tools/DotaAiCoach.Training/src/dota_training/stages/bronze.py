"""BRONZE stage: RAW parsed into records close to OpenDota's own shape.
Rejects obviously-broken responses; no business/domain filtering yet
(that belongs to FEATURES, per dota-data's pipeline-stages.md)."""

from __future__ import annotations

import json
import logging

import pyarrow as pa
import pyarrow.parquet as pq

from dota_training.config import BRONZE_PATH, RAW_DIR
from dota_training.opendota.acquire import AVG_RANK_TIER_SIDECAR
from dota_training.stages.raw import iter_raw_match_envelopes, parse_match_body

log = logging.getLogger(__name__)

REQUIRED_MATCH_FIELDS = ("match_id", "duration", "radiant_win", "start_time", "game_mode", "lobby_type", "players")
REQUIRED_PLAYER_FIELDS = ("player_slot", "hero_id")


def is_well_formed(match: dict) -> bool:
    if not all(field in match and match[field] is not None for field in REQUIRED_MATCH_FIELDS):
        return False
    if not isinstance(match["players"], list) or len(match["players"]) != 10:
        return False
    return all(all(field in p for field in REQUIRED_PLAYER_FIELDS) for p in match["players"])


def build_bronze(raw_dir=RAW_DIR, output_path=BRONZE_PATH) -> int:
    records = []
    rejected = 0

    avg_rank_tiers: dict[str, float | None] = {}
    sidecar_path = raw_dir / AVG_RANK_TIER_SIDECAR.name
    if sidecar_path.exists():
        avg_rank_tiers = json.loads(sidecar_path.read_text(encoding="utf-8"))

    for envelope in iter_raw_match_envelopes(raw_dir):
        match = parse_match_body(envelope)
        if match is None or not is_well_formed(match):
            rejected += 1
            continue

        records.append(
            {
                "match_id": match["match_id"],
                "duration": match["duration"],
                "radiant_win": match["radiant_win"],
                "start_time": match["start_time"],
                "game_mode": match["game_mode"],
                "lobby_type": match["lobby_type"],
                "patch": match.get("patch"),
                "first_blood_time": match.get("first_blood_time"),
                "avg_rank_tier": avg_rank_tiers.get(str(match["match_id"])),
                # Full nested structures preserved as JSON strings — BRONZE
                # is "is this well-formed", not "reshape it yet". NORMALIZED
                # and FEATURES do the reshaping.
                "raw_json": envelope["body"],
                "raw_fetched_at_utc": envelope["fetchedAtUtc"],
            }
        )

    log.info("BRONZE: %d well-formed matches, %d rejected", len(records), rejected)

    table = pa.Table.from_pylist(records)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    pq.write_table(table, output_path)
    return len(records)


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    build_bronze()
