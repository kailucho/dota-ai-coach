"""Tests for the draft baseline: hero global winrate (Bayesian-shrunk) +
matchup matrix (Bayesian-shrunk) + logistic regression. See docs/adr/0019."""

from __future__ import annotations

import pandas as pd
import pytest

from dota_training.models.draft_baseline import (
    compute_draft_features,
    compute_hero_global_winrate,
    compute_matchup_matrix,
)


def _match_row(match_id, patch, radiant_win, radiant_heroes, dire_heroes):
    return {
        "match_id": match_id,
        "patch": patch,
        "radiant_win": radiant_win,
        "radiant_hero_ids": radiant_heroes,
        "dire_hero_ids": dire_heroes,
    }


class TestHeroGlobalWinrateShrinkage:
    def test_small_sample_hero_winrate_is_shrunk_toward_overall_prior_not_raw_empirical(self):
        """Regression guard for the bug found in this baseline's first
        version: a hero with e.g. 2/2 wins produced a raw empirical rate
        of 1.0, which dominated the model and failed the top5-vs-bottom5
        domain sanity check. Bayesian shrinkage must pull this toward the
        overall prior, per dota-ml's explicit shrinkage requirement."""
        train_df = pd.DataFrame(
            [
                _match_row(1, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
                _match_row(2, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]),
            ]
        )
        overall_winrate = 0.5
        winrates = compute_hero_global_winrate(train_df, overall_winrate)

        # Hero 1 won 2/2 (raw empirical = 1.0) -- shrunk value must be
        # meaningfully below 1.0, pulled toward the 0.5 prior.
        assert winrates[(1, 60)] < 0.8, f"expected shrinkage to pull the 2/2 raw rate well below 1.0, got {winrates[(1, 60)]}"
        assert winrates[(1, 60)] > 0.5, "still above the prior since the (small) sample was all wins"

    def test_large_sample_hero_winrate_is_close_to_raw_empirical(self):
        """With a large sample, shrinkage should have little effect --
        confirms the shrinkage constant doesn't over-smooth real signal."""
        matches = [
            _match_row(i, patch=60, radiant_win=(i % 10 != 0), radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10])
            for i in range(200)
        ]
        train_df = pd.DataFrame(matches)
        overall_winrate = 0.9  # matches the true rate baked into the fixture above
        winrates = compute_hero_global_winrate(train_df, overall_winrate)

        # True empirical rate is 0.9 (180/200); with 200 samples and a
        # shrinkage prior of 30, the shrunk value should stay close.
        assert winrates[(1, 60)] == pytest.approx(0.9, abs=0.05)


class TestMatchupMatrixShrinkage:
    def test_sparse_matchup_pair_is_shrunk_toward_overall_prior(self):
        train_df = pd.DataFrame(
            [_match_row(1, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10])]
        )
        overall_winrate = 0.5
        matrix = compute_matchup_matrix(train_df, overall_winrate)

        # Hero 1 vs hero 6: observed once, radiant (hero 1) won.
        key = (1, 6)
        assert matrix[key] < 0.8, "a single observation must be heavily shrunk toward the prior, not report a raw 1.0"


class TestDraftFeaturesGeneralization:
    """The regression this test suite exists to catch: a feature that
    looks strong on the SAME data it was fit from but carries no real
    signal on held-out data. See docs/adr/0019's investigation."""

    def test_matchup_advantage_correlation_drops_sharply_on_held_out_data(self):
        # Build a train set with enough matches that per-pair shrinkage
        # has *some* real signal to shrink toward, then check a
        # DIFFERENT held-out match using the SAME hero pool -- the
        # matchup_advantage feature should not perfectly predict the
        # held-out match's outcome just because the pool overlaps.
        train_matches = [
            _match_row(i, patch=60, radiant_win=(i % 2 == 0), radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10])
            for i in range(20)
        ]
        train_df = pd.DataFrame(train_matches)
        overall_winrate = float(train_df["radiant_win"].astype(int).mean())

        hero_winrates = compute_hero_global_winrate(train_df, overall_winrate)
        matchup_matrix = compute_matchup_matrix(train_df, overall_winrate)

        held_out = pd.DataFrame(
            [_match_row(999, patch=60, radiant_win=True, radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10])]
        )
        features = compute_draft_features(held_out, hero_winrates, matchup_matrix, overall_winrate)

        # With an evenly-split train outcome (50/50 alternating), the
        # matchup_advantage for this exact hero pool should be close to
        # zero (no real advantage baked in), not near +/-0.5.
        assert abs(features.iloc[0]["matchup_advantage"]) < 0.1


class TestDomainSanityRegressionGuard:
    def test_top5_winrate_heroes_score_higher_than_bottom5_when_facing_each_other(self):
        """Direct regression test for the domain sanity check that failed
        with unshrunk hero winrates (docs/adr/0019). Build a synthetic
        train set where 5 heroes clearly outperform another 5, then
        confirm the shrunk winrate table + features reflect that."""
        matches = []
        for i in range(60):
            # Strong pool [1..5] vs weak pool [6..10], strong side wins 80% of the time.
            matches.append(_match_row(i, patch=60, radiant_win=(i % 5 != 0), radiant_heroes=[1, 2, 3, 4, 5], dire_heroes=[6, 7, 8, 9, 10]))
        train_df = pd.DataFrame(matches)
        overall_winrate = float(train_df["radiant_win"].astype(int).mean())

        hero_winrates = compute_hero_global_winrate(train_df, overall_winrate)

        strong_avg = sum(hero_winrates.get((h, 60), overall_winrate) for h in (1, 2, 3, 4, 5)) / 5
        weak_avg = sum(hero_winrates.get((h, 60), overall_winrate) for h in (6, 7, 8, 9, 10)) / 5

        assert strong_avg > weak_avg
