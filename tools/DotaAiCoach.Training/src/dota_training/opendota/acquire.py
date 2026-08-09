"""RAW acquisition: the 3-step funnel documented in docs/adr/0017.

1. /parsedMatches walked backwards from several match_id anchors ->
   candidates already parsed by OpenDota (free).
2. /explorer, batched ~500 IDs per call, filtered to Ranked All Draft
   (game_mode=22, lobby_type=7 -- NOT 22=Turbo, see config.py's comment)
   with a sane duration range -> ~1 request per 500 candidates.
3. GET /matches/{id} for survivors only -- already parsed, 1 request each,
   no POST /request + poll cost.

Anchors are spread across roughly the last 6 months of match_id space so
the resulting dataset actually spans real calendar time, which the 4-way
temporal split in stages/dataset.py depends on.
"""

from __future__ import annotations

import json
import logging
import sys
from dataclasses import dataclass

import requests

from dota_training.config import (
    MAX_DURATION_SECONDS,
    MIN_DURATION_SECONDS,
    RANKED_GAME_MODE,
    RANKED_LOBBY_TYPE,
    RAW_DIR,
    TARGET_MATCH_COUNT,
)
from dota_training.opendota.client import OpenDotaClient

AVG_RANK_TIER_SIDECAR = RAW_DIR / "avg_rank_tier_by_match.json"

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s")
log = logging.getLogger(__name__)

PARSED_MATCHES_PAGE_SIZE = 100
EXPLORER_BATCH_SIZE = 500


@dataclass
class AcquisitionResult:
    anchor_match_id: int
    candidates_seen: int
    ranked_survivors: int
    matches_fetched: int


def collect_parsed_candidates(client: OpenDotaClient, anchor_match_id: int, pages: int) -> list[int]:
    """Walks /parsedMatches backwards from anchor_match_id, collecting match_ids."""
    ids: list[int] = []
    cursor = anchor_match_id
    for _ in range(pages):
        rows = client.get_json("/parsedMatches", {"less_than_match_id": cursor}, force_refetch=True)
        if not rows:
            break
        page_ids = [r["match_id"] for r in rows]
        ids.extend(page_ids)
        cursor = min(page_ids)
    return ids


def filter_ranked(client: OpenDotaClient, candidate_ids: list[int]) -> list[dict]:
    """Batches candidate_ids through /explorer, keeping Ranked All Draft matches in the target duration range."""
    survivors: list[dict] = []
    for i in range(0, len(candidate_ids), EXPLORER_BATCH_SIZE):
        batch = candidate_ids[i : i + EXPLORER_BATCH_SIZE]
        id_list = ",".join(str(x) for x in batch)
        sql = (
            "select match_id, lobby_type, game_mode, duration, radiant_win, start_time, avg_rank_tier "
            f"from public_matches where match_id in ({id_list}) "
            f"and lobby_type = {RANKED_LOBBY_TYPE} and game_mode = {RANKED_GAME_MODE} "
            f"and duration between {MIN_DURATION_SECONDS} and {MAX_DURATION_SECONDS}"
        )
        rows = client.explorer_sql(sql)
        survivors.extend(rows)

    _persist_avg_rank_tiers(survivors)
    return survivors


def _persist_avg_rank_tiers(rows: list[dict]) -> None:
    """/matches/{id} doesn't carry avg_rank_tier (it's public_matches-only,
    from /explorer) -- persisted as a small sidecar so BRONZE can join it
    back onto each match without a second network call."""
    RAW_DIR.mkdir(parents=True, exist_ok=True)
    existing: dict[str, float | None] = {}
    if AVG_RANK_TIER_SIDECAR.exists():
        existing = json.loads(AVG_RANK_TIER_SIDECAR.read_text(encoding="utf-8"))

    for row in rows:
        existing[str(row["match_id"])] = row.get("avg_rank_tier")

    AVG_RANK_TIER_SIDECAR.write_text(json.dumps(existing), encoding="utf-8")


def fetch_parsed_matches(client: OpenDotaClient, match_ids: list[int]) -> int:
    """Fetches full parsed match detail for each ID, writing RAW envelopes.
    Skips (does not fail the whole run on) an individual match's transient
    server error -- a single flaky OpenDota response should not lose
    everything else already fetched this run. Returns count actually
    fetched (new, not from cache)."""
    fetched = 0
    failed = 0
    for match_id in match_ids:
        try:
            client.get_json(f"/matches/{match_id}")
            fetched += 1
        except requests.exceptions.HTTPError as exc:
            failed += 1
            log.warning("skipping match %d after fetch error: %s", match_id, exc)
        if (fetched + failed) % 100 == 0:
            log.info("fetched %d/%d parsed matches (%d failed)", fetched, len(match_ids), failed)
    if failed:
        log.info("fetch_parsed_matches: %d/%d failed and were skipped", failed, len(match_ids))
    return fetched


def acquire(anchors: list[int], pages_per_anchor: int, target_matches: int = TARGET_MATCH_COUNT) -> list[AcquisitionResult]:
    client = OpenDotaClient()
    results: list[AcquisitionResult] = []
    collected_ids: set[int] = set()

    for anchor in anchors:
        if len(collected_ids) >= target_matches:
            break

        log.info("anchor %d: collecting parsed-match candidates", anchor)
        candidates = collect_parsed_candidates(client, anchor, pages_per_anchor)
        log.info("anchor %d: %d candidates, filtering via /explorer", anchor, len(candidates))
        survivors = filter_ranked(client, candidates)
        log.info(
            "anchor %d: yield %d/%d (%.1f%%)",
            anchor,
            len(survivors),
            len(candidates),
            100.0 * len(survivors) / max(1, len(candidates)),
        )

        new_ids = [s["match_id"] for s in survivors if s["match_id"] not in collected_ids]
        remaining_budget = target_matches - len(collected_ids)
        new_ids = new_ids[:remaining_budget]

        fetched = fetch_parsed_matches(client, new_ids)
        collected_ids.update(new_ids)

        results.append(
            AcquisitionResult(
                anchor_match_id=anchor,
                candidates_seen=len(candidates),
                ranked_survivors=len(survivors),
                matches_fetched=fetched,
            )
        )

    log.info("acquisition complete: %d ranked matches in RAW cache", len(collected_ids))
    return results


if __name__ == "__main__":
    # Anchors must be provided explicitly by whoever runs this — see
    # README.md for how to pick current match_id anchors (e.g. via
    # /parsedMatches with no less_than_match_id for "now", and /explorer
    # date-to-match_id lookups for older anchors). No hardcoded anchor
    # here: match_id space keeps moving, a baked-in value would go stale.
    log.error(
        "Run via a script that supplies --anchors explicitly; "
        "see docs/adr/0017 for how anchors were chosen for this baseline's dataset."
    )
    sys.exit(1)
