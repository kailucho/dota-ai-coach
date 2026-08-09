"""Verifies the 4-way temporal split's ordering and per-match integrity guarantees."""

from __future__ import annotations

import pandas as pd
import pytest

from dota_training.stages.dataset import build_dataset


@pytest.fixture
def synthetic_features(tmp_path):
    """100 synthetic matches, 3 timestamp rows each, match_id monotonic with start_time."""
    rows = []
    for match_id in range(1000, 1100):
        start_time = 1_700_000_000 + match_id * 100
        for t in (5, 10, 15):
            rows.append(
                {
                    "match_id": match_id,
                    "minute_T": t,
                    "gold_adv_at_T": match_id % 7 - 3,
                    "patch": 60,
                    "start_time": start_time,
                    "radiant_win": match_id % 2 == 0,
                    "radiant_hero_ids": [1, 2, 3, 4, 5],
                    "dire_hero_ids": [6, 7, 8, 9, 10],
                }
            )
    df = pd.DataFrame(rows)
    path = tmp_path / "features.parquet"
    df.to_parquet(path)
    return path


class TestSplitOrdering:
    def test_splits_are_temporally_ordered(self, synthetic_features, tmp_path):
        metadata = build_dataset(features_path=synthetic_features, datasets_dir=tmp_path / "datasets", dataset_id="test-ds")

        order = ["train", "val", "calib", "test"]
        for earlier, later in zip(order, order[1:]):
            earlier_max = metadata["splits"][earlier]["max_start_time"]
            later_min = metadata["splits"][later]["min_start_time"]
            assert earlier_max is not None and later_min is not None
            assert earlier_max <= later_min, f"{earlier} overlaps {later} in time"

    def test_no_match_spans_two_splits(self, synthetic_features, tmp_path):
        build_dataset(features_path=synthetic_features, datasets_dir=tmp_path / "datasets", dataset_id="test-ds")

        seen_in: dict[int, list[str]] = {}
        for split_name in ("train", "val", "calib", "test"):
            split_df = pd.read_parquet(tmp_path / "datasets" / "test-ds" / f"{split_name}.parquet")
            for match_id in split_df["match_id"].unique():
                seen_in.setdefault(match_id, []).append(split_name)

        spanning = {mid: splits for mid, splits in seen_in.items() if len(splits) > 1}
        assert not spanning, f"matches appearing in multiple splits: {spanning}"

    def test_record_counts_match_actual_row_counts(self, synthetic_features, tmp_path):
        metadata = build_dataset(features_path=synthetic_features, datasets_dir=tmp_path / "datasets", dataset_id="test-ds")

        for split_name in ("train", "val", "calib", "test"):
            split_df = pd.read_parquet(tmp_path / "datasets" / "test-ds" / f"{split_name}.parquet")
            assert len(split_df) == metadata["record_counts"][split_name]

    def test_all_matches_accounted_for_across_splits(self, synthetic_features, tmp_path):
        metadata = build_dataset(features_path=synthetic_features, datasets_dir=tmp_path / "datasets", dataset_id="test-ds")

        total_matches = sum(s["match_count"] for s in metadata["splits"].values())
        assert total_matches == metadata["total_matches"]
