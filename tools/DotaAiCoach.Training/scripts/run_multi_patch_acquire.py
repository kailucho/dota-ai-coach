"""One-off runner for the ADR-0021 multi-patch acquisition expansion.

Anchors chosen and verified live (see docs/adr/0021). IMPORTANT
correction found during this run: OpenDota's /explorer endpoint queries
the `public_matches` table, which is NOT full history -- it's a rolling
window. Verified live: `select min(match_id), max(match_id) from
public_matches` returned a floor of match_id 8410426907 (patch 58,
2025-08-10), not patch 58's actual start (2025-05-22). The original
anchors (8317301346, 8343239381) were below this floor and returned 0
survivors from step 2 of the 3-step funnel. Anchors below are re-picked
to stay within the retained window while still landing in patch 58/59:

  - patch 58 (7.39, 2025-05-22 .. 2025-12-16, but only 2025-08-10 onward
    is reachable via /explorer): 8442382432 (~2025-08-20),
    8478688338 (2025-09-24, confirmed patch=58 live)
  - patch 59 (7.40, 2025-12-16 .. 2026-03-24): 8612740914 (2025-12-20,
    confirmed patch=59 live), 8672785297 (2026-02-01, confirmed patch=59 live)
patch 60 (7.41, current) is already well covered (1535 matches) by the
existing seed and is not re-targeted here.

Existing RAW cache (1912 matches, patch 60/59 mixed) is reused as-is --
OpenDotaClient.get_json() is on-disk-cache-first, so nothing already
fetched is re-fetched. This script only adds new match_ids on top.

Run with: PYTHONPATH=src .venv/Scripts/python.exe scripts/run_multi_patch_acquire.py
"""

from __future__ import annotations

import logging

from dota_training.opendota.acquire import acquire

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s")
log = logging.getLogger(__name__)

# Interleaved patch-58/patch-59 anchors so a killed/interrupted run still
# yields a roughly balanced partial result rather than fully completing
# one patch before starting the next.
ANCHORS = [
    8442382432,  # patch 58, ~2025-08-20 (within /explorer's retained window)
    8612740914,  # patch 59, 2025-12-20
    8478688338,  # patch 58, 2025-09-24 (confirmed patch=58 live)
    8672785297,  # patch 59, 2026-02-01
]

# pages_per_anchor: PARSED_MATCHES_PAGE_SIZE=100/page. ~12 pages/anchor
# budgets ~1200 candidates per anchor, matching the ~19-78% ranked-survivor
# yield range observed in ADR-0017 to comfortably clear a few hundred
# ranked survivors per anchor.
PAGES_PER_ANCHOR = 14

# Target additional matches across all 4 anchors combined (on top of the
# existing 1912 already in RAW). acquire()'s target_matches is a *global*
# cap on collected_ids, so this is the desired NEW total across new anchors
# (existing collected_ids from prior runs are not pre-seeded into acquire's
# in-memory set, since RAW-cache reuse already makes re-fetching a no-op --
# acquire() will just find match_ids not yet locally cached and fetch those).
TARGET_NEW_MATCHES = 2000

if __name__ == "__main__":
    results = acquire(ANCHORS, PAGES_PER_ANCHOR, target_matches=TARGET_NEW_MATCHES)
    for r in results:
        log.info(
            "anchor=%d candidates=%d survivors=%d fetched=%d",
            r.anchor_match_id,
            r.candidates_seen,
            r.ranked_survivors,
            r.matches_fetched,
        )
