"""FEATURES stage: NORMALIZED -> per-(match, timestamp) feature rows.

Business/domain filtering (Turbo/Ranked, abandons) applies HERE, per
dota-data's "or earlier, never later" rule. Every feature is computed
using ONLY event/array data with time/index <= T -- this is the discipline
tests/test_leakage.py's re-derivation test verifies mechanically.
"""

from __future__ import annotations

import json
import logging

import pandas as pd
import pyarrow as pa
import pyarrow.parquet as pq

from dota_training.config import (
    FEATURES_PATH,
    KEY_ITEM_KEYS,
    MAX_LEAVER_STATUS,
    MIN_MINUTES_BEFORE_MATCH_END,
    NORMALIZED_PATH,
    RANKED_GAME_MODE,
    RANKED_LOBBY_TYPE,
    TIMESTAMPS_MINUTES,
)

log = logging.getLogger(__name__)

RADIANT_SLOTS = {0, 1, 2, 3, 4}


def _is_radiant(player_slot: int) -> bool:
    return player_slot in RADIANT_SLOTS


def _side_sum(players: list[dict], key: str, index: int) -> tuple[float, float]:
    radiant = sum(p[key][index] for p in players if _is_radiant(p["player_slot"]) and p.get(key) and len(p[key]) > index)
    dire = sum(p[key][index] for p in players if not _is_radiant(p["player_slot"]) and p.get(key) and len(p[key]) > index)
    return radiant, dire


def _gini(values: list[float]) -> float:
    """Gini coefficient of a small list of non-negative values (0 = perfectly even, ->1 = concentrated)."""
    n = len(values)
    if n == 0 or sum(values) == 0:
        return 0.0
    sorted_values = sorted(values)
    cumulative = sum((i + 1) * v for i, v in enumerate(sorted_values))
    return (2 * cumulative) / (n * sum(sorted_values)) - (n + 1) / n


def compute_features_for_match(match: dict, timestamps_minutes: list[int], avg_rank_tier: float | None = None) -> list[dict]:
    """Computes one feature row per usable timestamp for a single parsed match.
    This function is the exact target of the leakage re-derivation test:
    truncating the match to data available at T and recomputing must be
    a no-op for every feature it returns."""
    duration_minutes = match["duration"] // 60
    max_t = duration_minutes - MIN_MINUTES_BEFORE_MATCH_END
    usable_ts = [t for t in timestamps_minutes if t <= max_t]
    if not usable_ts:
        return []

    players = match["players"]
    radiant_gold_adv = match.get("radiant_gold_adv") or []
    radiant_xp_adv = match.get("radiant_xp_adv") or []
    objectives = match.get("objectives") or []
    teamfights = match.get("teamfights") or []

    rows = []
    for t in usable_ts:
        cutoff_seconds = t * 60

        gold_adv_at_t = radiant_gold_adv[t] if t < len(radiant_gold_adv) else None
        xp_adv_at_t = radiant_xp_adv[t] if t < len(radiant_xp_adv) else None

        prior_t = max(0, t - 5)
        gold_adv_slope_5 = (
            radiant_gold_adv[t] - radiant_gold_adv[prior_t]
            if t < len(radiant_gold_adv) and prior_t < len(radiant_gold_adv)
            else None
        )
        xp_adv_slope_5 = (
            radiant_xp_adv[t] - radiant_xp_adv[prior_t]
            if t < len(radiant_xp_adv) and prior_t < len(radiant_xp_adv)
            else None
        )

        lh_radiant, lh_dire = _side_sum(players, "lh_t", t)
        lh_diff_at_t = lh_radiant - lh_dire

        gold_radiant_values = [p["gold_t"][t] for p in players if _is_radiant(p["player_slot"]) and p.get("gold_t") and len(p["gold_t"]) > t]
        gold_dire_values = [p["gold_t"][t] for p in players if not _is_radiant(p["player_slot"]) and p.get("gold_t") and len(p["gold_t"]) > t]
        gold_gini_radiant = _gini(gold_radiant_values)
        gold_gini_dire = _gini(gold_dire_values)
        max_hero_gold_diff = (max(gold_radiant_values, default=0)) - (max(gold_dire_values, default=0))

        kills_radiant = sum(
            1 for p in players if _is_radiant(p["player_slot"]) for k in (p.get("kills_log") or []) if k["time"] <= cutoff_seconds
        )
        kills_dire = sum(
            1 for p in players if not _is_radiant(p["player_slot"]) for k in (p.get("kills_log") or []) if k["time"] <= cutoff_seconds
        )
        kills_diff_at_t = kills_radiant - kills_dire

        recent_cutoff = max(0, cutoff_seconds - 300)
        deaths_recent_radiant = sum(
            1
            for p in players
            if _is_radiant(p["player_slot"])
            for k in (p.get("kills_log") or [])
            if recent_cutoff < k["time"] <= cutoff_seconds
        )
        deaths_recent_dire = sum(
            1
            for p in players
            if not _is_radiant(p["player_slot"])
            for k in (p.get("kills_log") or [])
            if recent_cutoff < k["time"] <= cutoff_seconds
        )
        deaths_in_last_5min_diff = deaths_recent_radiant - deaths_recent_dire

        teamfights_before_t = [tf for tf in teamfights if tf.get("start", 0) <= cutoff_seconds]
        tf_radiant_won = 0
        tf_dire_won = 0
        for tf in teamfights_before_t:
            tf_players = tf.get("players") or []
            radiant_gold_delta = sum(
                tf_players[i].get("gold_delta", 0) for i, p in enumerate(players) if _is_radiant(p["player_slot"]) and i < len(tf_players)
            )
            dire_gold_delta = sum(
                tf_players[i].get("gold_delta", 0) for i, p in enumerate(players) if not _is_radiant(p["player_slot"]) and i < len(tf_players)
            )
            if radiant_gold_delta > dire_gold_delta:
                tf_radiant_won += 1
            elif dire_gold_delta > radiant_gold_delta:
                tf_dire_won += 1
        teamfights_won_diff = tf_radiant_won - tf_dire_won

        towers_radiant = sum(
            1
            for o in objectives
            if o.get("type") == "building_kill" and "tower" in (o.get("key") or "") and o["time"] <= cutoff_seconds and o.get("player_slot", -1) < 128
        )
        towers_dire = sum(
            1
            for o in objectives
            if o.get("type") == "building_kill" and "tower" in (o.get("key") or "") and o["time"] <= cutoff_seconds and o.get("player_slot", 999) >= 128
        )
        towers_killed_diff = towers_radiant - towers_dire

        barracks_radiant = sum(
            1
            for o in objectives
            if o.get("type") == "building_kill" and "rax" in (o.get("key") or "") and o["time"] <= cutoff_seconds and o.get("player_slot", -1) < 128
        )
        barracks_dire = sum(
            1
            for o in objectives
            if o.get("type") == "building_kill" and "rax" in (o.get("key") or "") and o["time"] <= cutoff_seconds and o.get("player_slot", 999) >= 128
        )
        barracks_killed_diff = barracks_radiant - barracks_dire

        roshan_radiant = sum(
            1
            for o in objectives
            if o.get("type") == "CHAT_MESSAGE_ROSHAN_KILL" and o["time"] <= cutoff_seconds and o.get("team") == 2
        )
        roshan_dire = sum(
            1
            for o in objectives
            if o.get("type") == "CHAT_MESSAGE_ROSHAN_KILL" and o["time"] <= cutoff_seconds and o.get("team") == 3
        )
        roshan_killed_diff = roshan_radiant - roshan_dire

        first_blood_time = match.get("first_blood_time")
        first_blood_event = next((o for o in objectives if o.get("type") == "CHAT_MESSAGE_FIRSTBLOOD"), None)
        first_blood_radiant = None
        if first_blood_time is not None and first_blood_time <= cutoff_seconds and first_blood_event is not None:
            slot = first_blood_event.get("player_slot")
            first_blood_radiant = _is_radiant(slot) if slot is not None else None

        key_item_radiant = sum(
            1
            for p in players
            if _is_radiant(p["player_slot"])
            for purchase in (p.get("purchase_log") or [])
            if purchase.get("key") in KEY_ITEM_KEYS and (purchase.get("time") or 0) <= cutoff_seconds
        )
        key_item_dire = sum(
            1
            for p in players
            if not _is_radiant(p["player_slot"])
            for purchase in (p.get("purchase_log") or [])
            if purchase.get("key") in KEY_ITEM_KEYS and (purchase.get("time") or 0) <= cutoff_seconds
        )
        key_item_count_diff = key_item_radiant - key_item_dire

        rows.append(
            {
                "match_id": match["match_id"],
                "minute_T": t,
                "gold_adv_at_T": gold_adv_at_t,
                "xp_adv_at_T": xp_adv_at_t,
                "gold_adv_slope_5": gold_adv_slope_5,
                "xp_adv_slope_5": xp_adv_slope_5,
                "lh_diff_at_T": lh_diff_at_t,
                "gold_gini_radiant": gold_gini_radiant,
                "gold_gini_dire": gold_gini_dire,
                "max_hero_gold_diff": max_hero_gold_diff,
                "kills_diff_at_T": kills_diff_at_t,
                "deaths_in_last_5min_diff": deaths_in_last_5min_diff,
                "teamfights_won_diff": teamfights_won_diff,
                "towers_killed_diff": towers_killed_diff,
                "barracks_killed_diff": barracks_killed_diff,
                "roshan_killed_diff": roshan_killed_diff,
                "first_blood_radiant": first_blood_radiant,
                "key_item_count_diff": key_item_count_diff,
                "radiant_hero_ids": [p["hero_id"] for p in players if _is_radiant(p["player_slot"])],
                "dire_hero_ids": [p["hero_id"] for p in players if not _is_radiant(p["player_slot"])],
                "avg_rank_tier": avg_rank_tier,
                "patch": match.get("patch"),
                "start_time": match.get("start_time"),
                # The label. Never used to compute any feature above --
                # attached only as the row's target column. Optional here
                # so the leakage re-derivation test can delete it from a
                # truncated match (simulating "not yet known") without
                # this function crashing.
                "radiant_win": match.get("radiant_win"),
            }
        )

    return rows


def build_features(normalized_path=NORMALIZED_PATH, output_path=FEATURES_PATH) -> int:
    table = pq.read_table(normalized_path)
    df = table.to_pandas()

    before = len(df)
    # Turbo/Ranked separation applied HERE (FEATURES stage), per
    # dota-data's "or earlier, never later" rule. game_mode==22 is Ranked
    # All Draft, NOT Turbo (Turbo is 23) -- verified against
    # /constants/game_mode; see docs/adr/0017 for the correction history.
    df = df[(df["game_mode"] == RANKED_GAME_MODE) & (df["lobby_type"] == RANKED_LOBBY_TYPE)]
    log.info("FEATURES: %d/%d matches are Ranked All Draft", len(df), before)

    all_rows = []
    rejected_abandoned = 0
    for _, row in df.iterrows():
        match = json.loads(row["raw_json"])

        leaver_statuses = [p.get("leaver_status", 0) for p in match.get("players", [])]
        if any((s or 0) > MAX_LEAVER_STATUS for s in leaver_statuses):
            rejected_abandoned += 1
            continue

        all_rows.extend(compute_features_for_match(match, TIMESTAMPS_MINUTES, avg_rank_tier=row.get("avg_rank_tier")))

    log.info("FEATURES: dropped %d matches with an abandon (leaver_status > %d)", rejected_abandoned, MAX_LEAVER_STATUS)
    log.info("FEATURES: %d (match, timestamp) rows produced", len(all_rows))

    features_df = pd.DataFrame(all_rows)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    pq.write_table(pa.Table.from_pandas(features_df, preserve_index=False), output_path)
    return len(features_df)


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    build_features()
