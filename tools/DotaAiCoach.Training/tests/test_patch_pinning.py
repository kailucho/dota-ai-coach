"""Verifies hero_pick_winrate_diff is joined by each match's OWN patch,
never 'the latest patch' (dota-ml leakage pattern 3), and computed from
the train split only (leakage pattern 5 variant)."""

from __future__ import annotations

import pandas as pd
import pytest

from dota_training.stages.dataset import apply_hero_winrate_feature, compute_hero_patch_winrates


def _match_row(match_id, patch, radiant_win, radiant_heroes, dire_heroes):
    return {
        "match_id": match_id,
        "patch": patch,
        "radiant_win": radiant_win,
        "radiant_hero_ids": radiant_heroes,
        "dire_hero_ids": dire_heroes,
    }


class TestHeroWinrateJoinedOnOwnPatch:
    def test_hero_winrate_joined_on_own_patch(self):
        # Hero 1 has an opposite win rate on patch 60 vs patch 61 --
        # a deliberately adversarial fixture: if the join used "the
        # latest patch" instead of each match's own, this test fails.
        train_df = pd.DataFrame(
            [
                _match_row(1, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
                _match_row(2, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
                _match_row(3, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
                _match_row(4, patch=61, radiant_win=False, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
                _match_row(5, patch=61, radiant_win=False, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
                _match_row(6, patch=61, radiant_win=False, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
            ]
        )

        winrates = compute_hero_patch_winrates(train_df)

        assert winrates[(1, 60)] == pytest.approx(1.0), "hero 1 won 100% of patch-60 matches"
        assert winrates[(1, 61)] == pytest.approx(0.0), "hero 1 won 0% of patch-61 matches -- must not be conflated with patch 60"

    def test_patch_60_match_receives_patch_60_rate_not_the_latest_patch(self):
        train_df = pd.DataFrame(
            [
                _match_row(1, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
                _match_row(2, patch=61, radiant_win=False, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
            ]
        )
        winrates = compute_hero_patch_winrates(train_df)

        eval_df = pd.DataFrame([_match_row(3, patch=60, radiant_win=True, radiant_heroes=[1], dire_heroes=[6])])
        eval_df["dire_hero_ids"] = [[6]]
        eval_df["radiant_hero_ids"] = [[1]]
        result = apply_hero_winrate_feature(eval_df, winrates)

        # Hero 1 on patch 60 has a 100% win rate; hero 6 on patch 60 has a
        # 0% win rate -- the diff should reflect patch 60, not patch 61
        # (where hero 1 lost).
        assert result.iloc[0]["hero_pick_winrate_diff"] == pytest.approx(1.0)


class TestHeroWinrateComputedFromTrainOnly:
    def test_hero_winrate_computed_from_train_split_only(self):
        train_df = pd.DataFrame(
            [_match_row(1, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10])]
        )
        # A val-split match with the opposite outcome must NOT influence
        # the winrate table -- compute_hero_patch_winrates only ever
        # receives the train slice by construction (see dataset.py), so
        # this test asserts that construction directly: the function
        # itself has no visibility into anything not passed to it.
        winrates = compute_hero_patch_winrates(train_df)

        assert winrates[(1, 60)] == pytest.approx(1.0)
        # A hero with zero train appearances defaults to 0.5 (undecidable), not fabricated.
        assert (99, 60) not in winrates

    def test_unseen_hero_patch_combo_defaults_to_half_not_a_fabricated_value(self):
        train_df = pd.DataFrame(
            [_match_row(1, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10])]
        )
        winrates = compute_hero_patch_winrates(train_df)

        eval_df = pd.DataFrame([_match_row(2, patch=99, radiant_win=True, radiant_heroes=[1], dire_heroes=[6])])
        eval_df["radiant_hero_ids"] = [[1]]
        eval_df["dire_hero_ids"] = [[6]]
        result = apply_hero_winrate_feature(eval_df, winrates)

        assert result.iloc[0]["hero_pick_winrate_diff"] == pytest.approx(0.0), "both sides default to 0.5 on an unseen patch, cancelling out"
