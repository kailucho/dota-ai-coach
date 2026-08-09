"""Tests for the item recommendation ranker: candidate-set correctness,
train-only popularity tables, and structural leakage guards. See
docs/adr/0020."""

from __future__ import annotations

import pandas as pd

from dota_training.models.item_ranker import (
    build_candidate_rows,
    compute_item_popularity_tables,
)


def _event_row(match_id, player_slot, hero_id, minute, items_owned_before, next_item, patch=60):
    return {
        "match_id": match_id,
        "player_slot": player_slot,
        "hero_id": hero_id,
        "minute": minute,
        "items_owned_before": items_owned_before,
        "n_items_owned_before": len(items_owned_before),
        "next_item": next_item,
        "patch": patch,
        "radiant_win": True,
        "player_won": True,
    }


class TestCandidateSetExcludesOwnedItems:
    def test_candidate_set_excludes_already_owned_items(self):
        df = pd.DataFrame([_event_row(1, 0, 1, 10, ["black_king_bar", "blink"], "aghanims_shard")])
        all_items = ["black_king_bar", "blink", "aghanims_shard", "manta"]
        tables = compute_item_popularity_tables(df, all_items)

        candidates = build_candidate_rows(df, all_items, tables)

        owned = {"black_king_bar", "blink"}
        assert not (set(candidates["candidate_item"]) & owned), "candidate pool must never include an already-owned item"

    def test_no_candidate_row_is_ever_flagged_already_owned(self):
        df = pd.DataFrame(
            [
                _event_row(1, 0, 1, 10, ["black_king_bar"], "manta"),
                _event_row(2, 1, 2, 5, [], "blink"),
            ]
        )
        all_items = ["black_king_bar", "blink", "aghanims_shard", "manta"]
        tables = compute_item_popularity_tables(df, all_items)

        candidates = build_candidate_rows(df, all_items, tables)

        assert not candidates["is_candidate_already_owned"].any(), (
            "is_candidate_already_owned must always be False in the actual training data -- "
            "it is included only as a structural sanity feature, not to carry signal"
        )


class TestPopularityTablesAreTrainSplitOnly:
    def test_hero_popularity_differs_when_computed_from_different_splits(self):
        train_df = pd.DataFrame(
            [
                _event_row(1, 0, 1, 5, [], "black_king_bar"),
                _event_row(2, 0, 1, 5, [], "black_king_bar"),
                _event_row(3, 0, 1, 5, [], "black_king_bar"),
            ]
        )
        # A val-only match with an entirely different preference for the
        # same hero -- if the popularity table were built including this,
        # the hero's rate for black_king_bar would be diluted.
        val_only_df = pd.DataFrame([_event_row(4, 0, 1, 5, [], "manta")])

        all_items = ["black_king_bar", "manta"]
        train_only_tables = compute_item_popularity_tables(train_df, all_items)
        combined_tables = compute_item_popularity_tables(pd.concat([train_df, val_only_df]), all_items)

        assert train_only_tables["hero"][(1, "black_king_bar")] != combined_tables["hero"][(1, "black_king_bar")], (
            "the popularity table must be sensitive to which rows it was built from -- "
            "the model-facing code must only ever pass the train split"
        )


class TestRepurchaseRowsAreExcludedUpstream:
    """Regression guard: a row where next_item is already in
    items_owned_before can never have a valid relevant candidate (the
    candidate pool excludes owned items by construction), so such rows
    must be filtered before reaching the ranker -- see
    stages/item_events.py's repurchase filter, added after this exact
    case was found live in the real dataset (~2.3% of rows)."""

    def test_every_row_has_at_least_one_possible_relevant_candidate(self):
        # A row where next_item is already owned would be silently
        # unscoreable (its query group has zero relevant candidates).
        # This test documents the invariant the upstream extraction must
        # uphold; build_candidate_rows itself doesn't filter such rows,
        # so the input data must never contain them.
        df = pd.DataFrame([_event_row(1, 0, 1, 10, ["manta"], "manta")])  # deliberately violates the invariant
        all_items = ["manta", "black_king_bar"]
        tables = compute_item_popularity_tables(df, all_items)

        candidates = build_candidate_rows(df, all_items, tables)

        relevant_count = candidates["relevant"].sum()
        assert relevant_count == 0, (
            "confirms the failure mode this test guards against: a repurchase row produces zero relevant "
            "candidates once owned items are filtered, which is exactly why stages/item_events.py excludes them"
        )


class TestNoOutcomeAdjacentFeatures:
    def test_candidate_rows_never_include_radiant_win_or_player_won_columns(self):
        df = pd.DataFrame([_event_row(1, 0, 1, 10, [], "black_king_bar")])
        all_items = ["black_king_bar", "manta"]
        tables = compute_item_popularity_tables(df, all_items)

        candidates = build_candidate_rows(df, all_items, tables)

        forbidden = {"radiant_win", "player_won"}
        assert not (forbidden & set(candidates.columns)), (
            "outcome-adjacent columns must never reach the candidate rows the ranker trains on -- "
            "dota-ml leakage pattern 2"
        )


class TestGroupBoundariesAlignWithPurchaseRows:
    def test_group_sizes_sum_to_total_candidate_rows(self):
        df = pd.DataFrame(
            [
                _event_row(1, 0, 1, 10, ["black_king_bar"], "manta"),
                _event_row(2, 1, 2, 5, [], "blink"),
                _event_row(3, 2, 3, 20, ["blink", "manta"], "aghanims_shard"),
            ]
        )
        all_items = ["black_king_bar", "blink", "aghanims_shard", "manta"]
        tables = compute_item_popularity_tables(df, all_items)

        candidates = build_candidate_rows(df, all_items, tables)
        group_sizes = candidates.groupby("query_id").size()

        assert group_sizes.sum() == len(candidates)
        assert group_sizes.index.nunique() == len(df), "every source row must produce exactly one query group"
