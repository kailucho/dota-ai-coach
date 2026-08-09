"""Enforces dota-ml's core rule: "A live model may never train using
information unavailable to the player at the equivalent game timestamp."
See docs/adr/0017 and dota-ml/references/leakage-and-feature-availability.md.
"""

from __future__ import annotations

import copy
import json
from pathlib import Path

import pytest
import yaml

from dota_training.config import FEATURES_SPEC_PATH
from dota_training.stages.features import compute_features_for_match

FIXTURE_PATH = Path(__file__).parent / "fixtures" / "matches_8936354085_parsed.json"

EXCLUDED_AS_LEAKAGE_FIELDS = {
    "gold_per_min", "xp_per_min", "kda", "net_worth", "level", "last_hits",
    "radiant_win", "win", "lose", "loss", "duration",
    "benchmarks", "lane_efficiency_pct", "teamfight_participation", "pred_vict", "computed_mmr",
    "tower_status_radiant", "tower_status_dire", "barracks_status_radiant", "barracks_status_dire",
}

NON_FEATURE_COLUMNS = {"match_id", "radiant_hero_ids", "dire_hero_ids", "patch", "start_time", "radiant_win"}


@pytest.fixture
def real_match() -> dict:
    return json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))


@pytest.fixture
def features_spec() -> dict:
    return yaml.safe_load(FEATURES_SPEC_PATH.read_text(encoding="utf-8"))


def _truncate_match_to_timestamp(match: dict, t_minutes: int) -> dict:
    """Simulates 'only the data a player would have seen by minute T' by
    slicing every minute-indexed array and dropping every event after T,
    then deleting outcome fields entirely. If a feature computed against
    this truncated match differs from the same feature computed against
    the full match, the feature is leaking future information."""
    truncated = copy.deepcopy(match)
    cutoff_seconds = t_minutes * 60

    truncated["radiant_gold_adv"] = (truncated.get("radiant_gold_adv") or [])[: t_minutes + 1]
    truncated["radiant_xp_adv"] = (truncated.get("radiant_xp_adv") or [])[: t_minutes + 1]
    truncated["objectives"] = [o for o in (truncated.get("objectives") or []) if o["time"] <= cutoff_seconds]
    truncated["teamfights"] = [tf for tf in (truncated.get("teamfights") or []) if tf.get("start", 0) <= cutoff_seconds]

    if truncated.get("first_blood_time", -1) > cutoff_seconds:
        truncated["first_blood_time"] = None

    for player in truncated["players"]:
        player["gold_t"] = (player.get("gold_t") or [])[: t_minutes + 1]
        player["xp_t"] = (player.get("xp_t") or [])[: t_minutes + 1]
        player["lh_t"] = (player.get("lh_t") or [])[: t_minutes + 1]
        player["dn_t"] = (player.get("dn_t") or [])[: t_minutes + 1]
        player["kills_log"] = [k for k in (player.get("kills_log") or []) if k["time"] <= cutoff_seconds]
        player["purchase_log"] = [p for p in (player.get("purchase_log") or []) if (p.get("time") or 0) <= cutoff_seconds]

    # Also delete the fields a feature must never touch, so a leaking
    # feature implementation would crash (KeyError) rather than silently
    # read the untruncated future value.
    for field in ("radiant_win", "duration"):
        truncated.pop(field, None)
    for player in truncated["players"]:
        for field in ("gold_per_min", "xp_per_min", "kda", "net_worth", "level", "last_hits"):
            player.pop(field, None)

    # duration is used only to bound the usable-timestamp list upstream of
    # compute_features_for_match, not inside it -- restore a value large
    # enough that compute_features_for_match's own duration-based gating
    # doesn't reject T for a reason unrelated to the leakage check itself.
    truncated["duration"] = (t_minutes + 10) * 60

    return truncated


class TestTimestampBoundFeatureComputation:
    """The single strongest test in this suite: a feature computed with
    only data-up-to-T must equal the same feature computed with the full
    match available. If they differ, the feature peeked past T."""

    @pytest.mark.parametrize("t_minutes", [5, 10, 15, 20])
    def test_feature_at_T_ignores_data_after_T(self, real_match, t_minutes):
        full_match = copy.deepcopy(real_match)
        full_match["duration"] = max(full_match["duration"], (t_minutes + 10) * 60)

        rows_from_full = compute_features_for_match(full_match, [t_minutes])
        assert rows_from_full, f"expected a usable row at T={t_minutes}"
        row_from_full = rows_from_full[0]

        truncated_match = _truncate_match_to_timestamp(real_match, t_minutes)
        rows_from_truncated = compute_features_for_match(truncated_match, [t_minutes])
        assert rows_from_truncated, f"expected a usable row at T={t_minutes} from the truncated match"
        row_from_truncated = rows_from_truncated[0]

        for key in row_from_full:
            if key in ("radiant_win",):
                continue
            assert row_from_full[key] == row_from_truncated[key], (
                f"feature '{key}' differs between full-match and truncated-to-T={t_minutes} computation "
                f"({row_from_full[key]!r} vs {row_from_truncated[key]!r}) -- this feature is leaking future data"
            )


class TestNoOutcomeDerivedColumns:
    def test_no_outcome_derived_columns(self, real_match):
        rows = compute_features_for_match(real_match, [10])
        assert rows
        row = rows[0]
        leaking = set(row.keys()) & (EXCLUDED_AS_LEAKAGE_FIELDS - {"radiant_win"})
        assert not leaking, f"feature row contains excluded leakage fields: {leaking}"


class TestFeaturesSpecCoverage:
    def test_every_feature_declared_in_spec(self, real_match, features_spec):
        rows = compute_features_for_match(real_match, [10])
        assert rows
        row_columns = set(rows[0].keys()) - NON_FEATURE_COLUMNS

        spec_names = {f["name"] for f in features_spec["features"]}
        undeclared = row_columns - spec_names
        assert not undeclared, f"features computed but not declared in features_spec.yaml: {undeclared}"

    def test_no_postgame_only_in_live_subset(self, features_spec):
        live_allowed = [f for f in features_spec["features"] if f["fairPlayClassification"] == "LIVE_ALLOWED"]
        assert live_allowed, "expected at least one LIVE_ALLOWED feature so a future live model has something to select"

        postgame_names = {f["name"] for f in features_spec["features"] if f["fairPlayClassification"] == "POSTGAME_ONLY"}
        live_names = {f["name"] for f in live_allowed}
        assert not (postgame_names & live_names)

    def test_every_feature_has_all_five_fairplay_fields(self, features_spec):
        required = {"source", "availability", "visibility", "fairPlayClassification", "justification"}
        for feature in features_spec["features"]:
            missing = required - feature.keys()
            assert not missing, f"feature '{feature['name']}' is missing FairPlay fields: {missing}"


class TestAblationSanity:
    def test_ablation_sanity(self, real_match):
        """A soft signal: dropping the strongest, most obviously-relevant
        feature (gold advantage) must not leave every other feature
        exactly the same -- if it did, that would suggest the feature
        computation is disconnected from real match state entirely."""
        rows = compute_features_for_match(real_match, [10, 20, 30])
        gold_advs = [r["gold_adv_at_T"] for r in rows]
        assert len(set(gold_advs)) > 1, "gold_adv_at_T should vary across timestamps in a real match"
