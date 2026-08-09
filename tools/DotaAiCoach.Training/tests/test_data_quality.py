"""Enforces dota-data's eight data-quality rules against this pipeline's actual code, not just by inspection."""

from __future__ import annotations

import json
from pathlib import Path

import pandas as pd
import pyarrow as pa
import pyarrow.parquet as pq
import pytest

from dota_training.config import RANKED_GAME_MODE, RANKED_LOBBY_TYPE
from dota_training.stages.bronze import is_well_formed
from dota_training.stages.features import build_features
from dota_training.stages.normalized import build_normalized

FIXTURE_PATH = Path(__file__).parent / "fixtures" / "matches_8936354085_parsed.json"


@pytest.fixture
def real_match() -> dict:
    return json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))


class TestGameModeCorrection:
    """Regression guard for the 22/23 correction documented in docs/adr/0017:
    game_mode 22 = Ranked All Draft, 23 = Turbo -- verified live against
    /constants/game_mode. An earlier draft of this pipeline had this
    backwards, which would have silently mixed Turbo into a 'ranked'
    dataset (dota-data rule 1). Do not revert this without re-verifying
    against /constants/game_mode."""

    def test_ranked_game_mode_constant_is_22_not_23(self):
        assert RANKED_GAME_MODE == 22, "22 is Ranked All Draft; 23 is Turbo -- verified against /constants/game_mode"

    def test_turbo_and_unranked_excluded(self, real_match, tmp_path):
        # The real fixture is itself Ranked All Draft (game_mode=22, lobby_type=7).
        assert real_match["game_mode"] == 22
        assert real_match["lobby_type"] == 7

        bronze_df = pd.DataFrame(
            [
                {
                    "match_id": real_match["match_id"],
                    "duration": real_match["duration"],
                    "radiant_win": real_match["radiant_win"],
                    "start_time": real_match["start_time"],
                    "game_mode": real_match["game_mode"],
                    "lobby_type": real_match["lobby_type"],
                    "patch": real_match["patch"],
                    "first_blood_time": real_match["first_blood_time"],
                    "raw_json": json.dumps(real_match),
                    "raw_fetched_at_utc": "2026-08-09T00:00:00Z",
                },
                {
                    # A synthetic Turbo match (game_mode=23) built from the
                    # same base data -- must be excluded at FEATURES stage.
                    "match_id": real_match["match_id"] + 1,
                    "duration": real_match["duration"],
                    "radiant_win": real_match["radiant_win"],
                    "start_time": real_match["start_time"] + 100,
                    "game_mode": 23,
                    "lobby_type": 0,
                    "patch": real_match["patch"],
                    "first_blood_time": real_match["first_blood_time"],
                    "raw_json": json.dumps({**real_match, "match_id": real_match["match_id"] + 1, "game_mode": 23, "lobby_type": 0}),
                    "raw_fetched_at_utc": "2026-08-09T00:00:00Z",
                },
            ]
        )

        normalized_path = tmp_path / "normalized.parquet"
        pq.write_table(pa.Table.from_pandas(bronze_df, preserve_index=False), normalized_path)

        features_path = tmp_path / "features.parquet"
        build_features(normalized_path=normalized_path, output_path=features_path)

        features_df = pd.read_parquet(features_path)
        assert real_match["match_id"] in features_df["match_id"].values
        assert (real_match["match_id"] + 1) not in features_df["match_id"].values, (
            "Turbo match (game_mode=23) leaked into the Ranked feature set"
        )


class TestAbandonExclusion:
    def test_abandoned_matches_excluded(self, real_match, tmp_path):
        abandoned_match = json.loads(json.dumps(real_match))  # deep copy via round-trip
        abandoned_match["match_id"] = real_match["match_id"] + 2
        abandoned_match["players"][0]["leaver_status"] = 2  # abandoned

        bronze_df = pd.DataFrame(
            [
                {
                    "match_id": abandoned_match["match_id"],
                    "duration": abandoned_match["duration"],
                    "radiant_win": abandoned_match["radiant_win"],
                    "start_time": abandoned_match["start_time"],
                    "game_mode": abandoned_match["game_mode"],
                    "lobby_type": abandoned_match["lobby_type"],
                    "patch": abandoned_match["patch"],
                    "first_blood_time": abandoned_match["first_blood_time"],
                    "raw_json": json.dumps(abandoned_match),
                    "raw_fetched_at_utc": "2026-08-09T00:00:00Z",
                }
            ]
        )
        normalized_path = tmp_path / "normalized.parquet"
        pq.write_table(pa.Table.from_pandas(bronze_df, preserve_index=False), normalized_path)

        features_path = tmp_path / "features.parquet"
        build_features(normalized_path=normalized_path, output_path=features_path)

        features_df = pd.read_parquet(features_path)
        assert "match_id" not in features_df.columns or abandoned_match["match_id"] not in features_df["match_id"].values


class TestBronzeWellFormedness:
    def test_well_formed_match_accepted(self, real_match):
        assert is_well_formed(real_match) is True

    def test_missing_required_field_rejected(self, real_match):
        broken = dict(real_match)
        del broken["radiant_win"]
        assert is_well_formed(broken) is False

    def test_wrong_player_count_rejected(self, real_match):
        broken = dict(real_match)
        broken["players"] = real_match["players"][:9]
        assert is_well_formed(broken) is False


class TestNormalizedDedup:
    def test_no_duplicate_match_ids(self, tmp_path):
        bronze_df = pd.DataFrame(
            [
                {
                    "match_id": 1,
                    "duration": 1800,
                    "radiant_win": True,
                    "start_time": 1_700_000_000,
                    "game_mode": 22,
                    "lobby_type": 7,
                    "patch": 60,
                    "first_blood_time": 30,
                    "raw_json": "{}",
                    "raw_fetched_at_utc": "2026-08-09T00:00:00Z",
                },
                {
                    "match_id": 1,  # duplicate
                    "duration": 1800,
                    "radiant_win": True,
                    "start_time": 1_700_000_000,
                    "game_mode": 22,
                    "lobby_type": 7,
                    "patch": 60,
                    "first_blood_time": 30,
                    "raw_json": "{}",
                    "raw_fetched_at_utc": "2026-08-09T01:00:00Z",  # fetched later -- should be kept
                },
            ]
        )
        bronze_path = tmp_path / "bronze.parquet"
        pq.write_table(pa.Table.from_pandas(bronze_df, preserve_index=False), bronze_path)

        normalized_path = tmp_path / "normalized.parquet"
        build_normalized(bronze_path=bronze_path, output_path=normalized_path)

        normalized_df = pd.read_parquet(normalized_path)
        assert len(normalized_df) == 1
        assert normalized_df.iloc[0]["raw_fetched_at_utc"] == "2026-08-09T01:00:00Z"

    def test_patch_and_start_time_non_null(self, tmp_path):
        bronze_df = pd.DataFrame(
            [
                {
                    "match_id": 1,
                    "duration": 1800,
                    "radiant_win": True,
                    "start_time": None,  # missing -- must be dropped
                    "game_mode": 22,
                    "lobby_type": 7,
                    "patch": 60,
                    "first_blood_time": 30,
                    "raw_json": "{}",
                    "raw_fetched_at_utc": "2026-08-09T00:00:00Z",
                },
                {
                    "match_id": 2,
                    "duration": 1800,
                    "radiant_win": True,
                    "start_time": 1_700_000_000,
                    "game_mode": 22,
                    "lobby_type": 7,
                    "patch": 60,
                    "first_blood_time": 30,
                    "raw_json": "{}",
                    "raw_fetched_at_utc": "2026-08-09T00:00:00Z",
                },
            ]
        )
        bronze_path = tmp_path / "bronze.parquet"
        pq.write_table(pa.Table.from_pandas(bronze_df, preserve_index=False), bronze_path)

        normalized_path = tmp_path / "normalized.parquet"
        build_normalized(bronze_path=bronze_path, output_path=normalized_path)

        normalized_df = pd.read_parquet(normalized_path)
        assert len(normalized_df) == 1
        assert normalized_df.iloc[0]["match_id"] == 2
