"""Unit tests for feature computation correctness (not leakage -- see test_leakage.py)."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from dota_training.stages.features import _gini, _is_radiant, compute_features_for_match

FIXTURE_PATH = Path(__file__).parent / "fixtures" / "matches_8936354085_parsed.json"


@pytest.fixture
def real_match() -> dict:
    return json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))


class TestPlayerSlotSides:
    @pytest.mark.parametrize("slot,expected", [(0, True), (1, True), (4, True), (128, False), (132, False)])
    def test_is_radiant(self, slot, expected):
        assert _is_radiant(slot) is expected


class TestGini:
    def test_even_distribution_has_zero_gini(self):
        assert _gini([100, 100, 100, 100, 100]) == pytest.approx(0.0, abs=1e-9)

    def test_concentrated_distribution_has_positive_gini(self):
        assert _gini([0, 0, 0, 0, 500]) > 0.5

    def test_empty_list_does_not_crash(self):
        assert _gini([]) == 0.0


class TestComputeFeaturesForMatch:
    def test_returns_one_row_per_usable_timestamp(self, real_match):
        # duration=1875s -> 31 minutes; MIN_MINUTES_BEFORE_MATCH_END=2 -> max_t=29
        rows = compute_features_for_match(real_match, [5, 10, 15, 20, 25, 30, 35, 40])
        timestamps = [r["minute_T"] for r in rows]
        assert 30 not in timestamps, "T=30 exceeds duration-2 for a 1875s match"
        assert 5 in timestamps
        assert 25 in timestamps

    def test_gold_adv_at_T_matches_the_source_array_directly(self, real_match):
        rows = compute_features_for_match(real_match, [10])
        assert rows[0]["gold_adv_at_T"] == real_match["radiant_gold_adv"][10]

    def test_no_usable_timestamps_returns_empty_list(self, real_match):
        very_short_match = dict(real_match)
        very_short_match["duration"] = 60  # 1 minute -- nothing survives the -2 minute gate
        rows = compute_features_for_match(very_short_match, [5, 10])
        assert rows == []

    def test_radiant_win_is_attached_as_label_not_used_as_feature_input(self, real_match):
        rows = compute_features_for_match(real_match, [10])
        assert rows[0]["radiant_win"] == real_match["radiant_win"]

    def test_hero_ids_are_captured_per_side(self, real_match):
        rows = compute_features_for_match(real_match, [10])
        assert len(rows[0]["radiant_hero_ids"]) == 5
        assert len(rows[0]["dire_hero_ids"]) == 5
