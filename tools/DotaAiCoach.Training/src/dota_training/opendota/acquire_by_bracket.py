"""Bracket-targeted RAW acquisition — a sibling to acquire.py's
/parsedMatches-based funnel, built to answer a specific gap: acquire.py's
funnel starts from /parsedMatches (already-parsed candidates), which
turned out to skew toward matches OpenDota's own community chose to
parse, not a representative sample of avg_rank_tier. Verified live against
OpenDota's own public_matches table (2026-08-10): a 1M-match_id window has
ample volume across every bracket (e.g. ~130k matches in the medal 4-6
band alone) — the scarcity in the existing dataset is a sampling-path
artifact, not a real data shortage.

This module queries /explorer directly for match_ids already inside the
target avg_rank_tier band (public_matches carries avg_rank_tier natively),
restricted to a match_id floor verified live to fall after patch 60's
start (patch 59 was observed at match_id 8700000000, patch 60 at
8750000000 — see docs/adr/0021 for the same floor-finding pattern applied
to /explorer's retained-window limit). This does not need
/parsedMatches at all — a match's own /matches/{id} response works
whether or not it was already in OpenDota's "parsed" list, since parsing
is triggered by DEMAND (any request for a match's full detail), not by
this project.
"""

from __future__ import annotations

import logging
from dataclasses import dataclass

from dota_training.config import (
    MAX_DURATION_SECONDS,
    MIN_DURATION_SECONDS,
    RANKED_GAME_MODE,
    RANKED_LOBBY_TYPE,
)
from dota_training.opendota.acquire import _persist_avg_rank_tiers, fetch_parsed_matches
from dota_training.opendota.client import OpenDotaClient

log = logging.getLogger(__name__)

EXPLORER_MATCH_ID_BATCH = 200_000  # verified live: a 2M window 400s (query too expensive / times out); 200k is comfortably inside what /explorer answers in one call

# Verified live (2026-08-10): patch 59 observed at match_id 8700000000,
# patch 60 at 8750000000 — see this module's docstring. A conservative
# floor comfortably inside patch 60, not the exact boundary.
PATCH_60_MATCH_ID_FLOOR = 8_750_000_000


@dataclass
class BracketAcquisitionResult:
    scanned_from: int
    scanned_to: int
    bracket_survivors: int
    matches_fetched: int


def find_bracket_candidates(
    client: OpenDotaClient,
    bracket_min: int,
    bracket_max: int,
    match_id_start: int,
    match_id_end: int,
) -> list[dict]:
    """One /explorer call, filtering directly on avg_rank_tier (public_matches'
    own column — no need to fetch a candidate's full detail just to learn
    its bracket, unlike acquire.py's funnel where bracket is unknown until
    step 2). bracket_min/max are avg_rank_tier TENS digits (e.g. 4-6 for
    Archon-Ancient), matching this project's existing avg_rank_tier // 10
    convention (see docs/ROADMAP.md)."""
    sql = (
        "select match_id, avg_rank_tier from public_matches "
        f"where lobby_type = {RANKED_LOBBY_TYPE} and game_mode = {RANKED_GAME_MODE} "
        f"and match_id between {match_id_start} and {match_id_end} "
        f"and duration between {MIN_DURATION_SECONDS} and {MAX_DURATION_SECONDS} "
        f"and (avg_rank_tier / 10)::int between {bracket_min} and {bracket_max}"
    )
    return client.explorer_sql(sql)


def acquire_by_bracket(
    bracket_min: int,
    bracket_max: int,
    target_matches: int,
    match_id_floor: int = PATCH_60_MATCH_ID_FLOOR,
) -> list[BracketAcquisitionResult]:
    """Scans forward from match_id_floor in EXPLORER_MATCH_ID_BATCH-sized
    windows, fetching full detail only for bracket-matching survivors,
    until target_matches new matches are collected or the live match_id
    ceiling is reached. Resumable exactly like acquire.py's funnel — RAW
    envelopes are cached to disk by OpenDotaClient.get_json, and this
    function's own progress (which windows scanned) is not separately
    persisted, so a re-run rescans from match_id_floor but re-fetches
    nothing already on disk (get_json's cache short-circuits the network
    call)."""
    client = OpenDotaClient()
    results: list[BracketAcquisitionResult] = []
    collected_ids: set[int] = set()

    ceiling = _live_match_id_ceiling(client)
    window_start = match_id_floor

    while len(collected_ids) < target_matches and window_start < ceiling:
        window_end = min(window_start + EXPLORER_MATCH_ID_BATCH, ceiling)
        log.info("scanning match_id [%d, %d) for bracket [%d, %d]...", window_start, window_end, bracket_min, bracket_max)

        survivors = find_bracket_candidates(client, bracket_min, bracket_max, window_start, window_end)
        log.info("window yield: %d bracket-matching candidates", len(survivors))

        new_ids = [s["match_id"] for s in survivors if s["match_id"] not in collected_ids]
        remaining_budget = target_matches - len(collected_ids)
        new_ids = new_ids[:remaining_budget]

        _persist_avg_rank_tiers(survivors)
        fetched = fetch_parsed_matches(client, new_ids)
        collected_ids.update(new_ids)

        results.append(
            BracketAcquisitionResult(
                scanned_from=window_start,
                scanned_to=window_end,
                bracket_survivors=len(survivors),
                matches_fetched=fetched,
            )
        )
        log.info("progress: %d/%d target matches collected", len(collected_ids), target_matches)

        window_start = window_end

    log.info("bracket acquisition complete: %d matches in RAW cache", len(collected_ids))
    return results


def _live_match_id_ceiling(client: OpenDotaClient) -> int:
    rows = client.explorer_sql(
        f"select max(match_id) as ceiling from public_matches where lobby_type = {RANKED_LOBBY_TYPE} and game_mode = {RANKED_GAME_MODE}"
    )
    return int(rows[0]["ceiling"])


if __name__ == "__main__":
    import sys

    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s")

    if len(sys.argv) < 4:
        log.error("usage: python -m dota_training.opendota.acquire_by_bracket <bracket_min> <bracket_max> <target_matches>")
        sys.exit(1)

    acquire_by_bracket(bracket_min=int(sys.argv[1]), bracket_max=int(sys.argv[2]), target_matches=int(sys.argv[3]))
