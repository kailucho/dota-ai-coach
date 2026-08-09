"""Extracts (state, next_item) training examples from parsed match
purchase_log data, for the item recommendation ranker (docs/adr/0020).

Per docs/MASTER_PLAN.md's Module 11: P(next_item | current_state).

Which purchases count as a meaningful "next item" decision is determined
by OpenDota's own item-quality tier (qual: rare/epic/artifact/common),
fetched from GET /constants/items and cached at
data/opendota_item_constants.json -- verified live rather than
hand-listed, since a manual blacklist attempt (an earlier version of this
file) missed several cheap-but-real components (ogre_axe, gauntlets,
mithril_hammer, ...) that dominated purchase_log by raw frequency without
being real "next item" decisions. qual=None (recipes), 'component'
(intermediate pieces like ogre_axe/blink's own base form), 'consumable',
and 'secret_shop' (raw stat items like platemail/demon_edge, rarely a
final purchase) are excluded.
"""

from __future__ import annotations

import json
import logging
from pathlib import Path

import pandas as pd
import pyarrow as pa
import pyarrow.parquet as pq

from dota_training.config import DATA_DIR, MAX_LEAVER_STATUS, NORMALIZED_PATH, RANKED_GAME_MODE, RANKED_LOBBY_TYPE

log = logging.getLogger(__name__)

ITEM_CONSTANTS_PATH = DATA_DIR / "opendota_item_constants.json"
MEANINGFUL_QUAL_TIERS = {"rare", "epic", "artifact", "common"}

RADIANT_SLOTS = {0, 1, 2, 3, 4}


def load_meaningful_item_keys(path: Path = ITEM_CONSTANTS_PATH) -> set[str]:
    constants = json.loads(path.read_text(encoding="utf-8"))
    return {key for key, item in constants.items() if item.get("qual") in MEANINGFUL_QUAL_TIERS}


def extract_item_events_for_match(match: dict, meaningful_item_keys: set[str]) -> list[dict]:
    """One row per (player, meaningful purchase), with the state (hero,
    minute, prior meaningful items owned) immediately BEFORE that
    purchase -- never including the purchase itself or anything after it,
    matching the win-probability model's timestamp-bound discipline."""
    rows = []
    for player in match.get("players", []):
        hero_id = player.get("hero_id")
        player_slot = player.get("player_slot")
        purchase_log = player.get("purchase_log") or []
        if hero_id is None or player_slot is None:
            continue

        meaningful_purchases = [p for p in purchase_log if p.get("key") in meaningful_item_keys]
        meaningful_purchases.sort(key=lambda p: p.get("time") or 0)

        owned_so_far: list[str] = []
        for purchase in meaningful_purchases:
            item_key = purchase["key"]
            time_seconds = purchase.get("time") or 0

            # A repurchase of an item already owned (e.g. a second Magic
            # Wand-tier item) is not a "next item" decision in the
            # candidate-ranking sense used by the item ranker -- the
            # candidate pool always excludes already-owned items, so this
            # row could never have a valid relevant candidate. Skip it
            # rather than silently producing an unscoreable row.
            if item_key not in owned_so_far:
                rows.append(
                    {
                        "match_id": match["match_id"],
                        "player_slot": player_slot,
                        "hero_id": hero_id,
                        "minute": max(0, time_seconds // 60),
                        "items_owned_before": list(owned_so_far),
                        "n_items_owned_before": len(owned_so_far),
                        "next_item": item_key,
                        "patch": match.get("patch"),
                        "radiant_win": match.get("radiant_win"),
                        "player_won": (player_slot in RADIANT_SLOTS) == bool(match.get("radiant_win")),
                    }
                )
            owned_so_far.append(item_key)

    return rows


def build_item_events(normalized_path=NORMALIZED_PATH, output_path=None) -> int:
    if output_path is None:
        output_path = DATA_DIR / "item_events.parquet"

    table = pq.read_table(normalized_path)
    df = table.to_pandas()

    before = len(df)
    df = df[(df["game_mode"] == RANKED_GAME_MODE) & (df["lobby_type"] == RANKED_LOBBY_TYPE)]
    log.info("ITEM_EVENTS: %d/%d matches are Ranked All Draft", len(df), before)

    meaningful_item_keys = load_meaningful_item_keys()
    log.info("ITEM_EVENTS: %d meaningful item keys (qual in %s)", len(meaningful_item_keys), MEANINGFUL_QUAL_TIERS)

    all_rows = []
    rejected_abandoned = 0
    for _, row in df.iterrows():
        match = json.loads(row["raw_json"])

        leaver_statuses = [p.get("leaver_status", 0) for p in match.get("players", [])]
        if any((s or 0) > MAX_LEAVER_STATUS for s in leaver_statuses):
            rejected_abandoned += 1
            continue

        all_rows.extend(extract_item_events_for_match(match, meaningful_item_keys))

    log.info("ITEM_EVENTS: dropped %d matches with an abandon", rejected_abandoned)
    log.info("ITEM_EVENTS: %d (player, purchase) rows produced", len(all_rows))

    events_df = pd.DataFrame(all_rows)
    # items_owned_before is a list column -- Arrow handles it, but store as
    # JSON string too for simplicity in the ranker's candidate generation.
    events_df["items_owned_before_json"] = events_df["items_owned_before"].apply(json.dumps)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    pq.write_table(pa.Table.from_pandas(events_df, preserve_index=False), output_path)
    return len(events_df)


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    build_item_events()
