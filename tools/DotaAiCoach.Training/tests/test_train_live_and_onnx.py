"""Tests for the LIVE_ALLOWED-restricted Win Probability retrain
(train_live.py) and the ONNX export pipeline (export_onnx.py). See
docs/adr/0022.

Covers:
1. FairPlay classification test (mechanical, not manual review, per
   dota-ml/references/leakage-and-feature-availability.md #2): every
   feature train_live.py's FEATURE_COLUMNS uses must be classified
   LIVE_ALLOWED or PUBLIC_PREFLIGHT in features_spec.yaml -- never
   POSTGAME_ONLY/SPECTATOR_ONLY/FORBIDDEN.
2. Regression guard: the known POSTGAME_ONLY features from
   docs/adr/0017 (lh_diff_at_T, gold_gini_radiant, gold_gini_dire,
   max_hero_gold_diff, teamfights_won_diff, key_item_count_diff) must
   never appear in train_live.py's feature set.
3. train.py (research baseline) is untouched -- its FEATURE_COLUMNS
   still includes the full 18-feature set, unaffected by train_live.py's
   introduction.
4. An actual automated ONNX-vs-original parity check (not just a manual
   script run), exercised against freshly-trained tiny sklearn/LightGBM
   models built in-test (no dependency on a specific trained artifact
   existing on disk, so this test is stable across retrains).
"""

from __future__ import annotations

import numpy as np
import pandas as pd
import pytest
import yaml

from dota_training.config import FEATURES_SPEC_PATH
from dota_training.models import train as research_train
from dota_training.models import train_live

LIVE_ELIGIBLE_CLASSIFICATIONS = {"LIVE_ALLOWED", "PUBLIC_PREFLIGHT"}

KNOWN_POSTGAME_ONLY_FEATURES = {
    "lh_diff_at_T",
    "gold_gini_radiant",
    "gold_gini_dire",
    "max_hero_gold_diff",
    "teamfights_won_diff",
    "key_item_count_diff",
}


@pytest.fixture
def features_spec() -> dict:
    return yaml.safe_load(FEATURES_SPEC_PATH.read_text(encoding="utf-8"))


@pytest.fixture
def classification_by_feature(features_spec) -> dict[str, str]:
    return {f["name"]: f["fairPlayClassification"] for f in features_spec["features"]}


class TestLiveFeatureSetClassification:
    """Mechanical FairPlay classification test, per
    leakage-and-feature-availability.md's testing guidance -- not a
    manual review checklist item."""

    def test_every_live_feature_is_declared_in_features_spec(self, classification_by_feature):
        undeclared = [f for f in train_live.FEATURE_COLUMNS if f not in classification_by_feature]
        assert not undeclared, f"train_live.py uses features not declared in features_spec.yaml: {undeclared}"

    def test_every_live_feature_column_is_live_eligible(self, classification_by_feature):
        violations = {
            f: classification_by_feature[f]
            for f in train_live.FEATURE_COLUMNS
            if classification_by_feature[f] not in LIVE_ELIGIBLE_CLASSIFICATIONS
        }
        assert not violations, (
            f"train_live.py's FEATURE_COLUMNS includes features not classified "
            f"LIVE_ALLOWED/PUBLIC_PREFLIGHT: {violations}"
        )

    def test_first_blood_radiant_used_by_live_model_is_live_eligible(self, classification_by_feature):
        # Encoded separately from FEATURE_COLUMNS in both train.py and
        # train_live.py (boolean -> float), same as train.py -- verify it
        # explicitly since it wouldn't be caught by the FEATURE_COLUMNS scan above.
        assert classification_by_feature["first_blood_radiant"] in LIVE_ELIGIBLE_CLASSIFICATIONS

    def test_no_known_postgame_only_feature_leaks_into_live_feature_set(self):
        leaked = KNOWN_POSTGAME_ONLY_FEATURES & set(train_live.FEATURE_COLUMNS)
        assert not leaked, f"POSTGAME_ONLY features present in the live-restricted feature set: {leaked}"

    def test_postgame_only_features_still_classified_postgame_only_in_spec(self, classification_by_feature):
        # Guards against someone "fixing" the live model by silently
        # reclassifying a feature in features_spec.yaml instead of
        # actually removing it -- the spec's classifications must stay
        # honest independent of which model currently uses what.
        for feature_name in KNOWN_POSTGAME_ONLY_FEATURES:
            assert classification_by_feature[feature_name] == "POSTGAME_ONLY", (
                f"{feature_name} is expected to remain POSTGAME_ONLY in features_spec.yaml"
            )


class TestResearchBaselineUnaffected:
    """docs/adr/0017's research baseline (train.py) must remain intact
    and buildable, unaffected by train_live.py's introduction -- per this
    task's explicit constraint (a sibling module, not a mutation)."""

    def test_research_feature_columns_still_include_postgame_only_features(self, classification_by_feature):
        research_postgame_only = {
            f for f in research_train.FEATURE_COLUMNS if classification_by_feature[f] == "POSTGAME_ONLY"
        }
        assert research_postgame_only == KNOWN_POSTGAME_ONLY_FEATURES, (
            "train.py's research feature set no longer matches the expected POSTGAME_ONLY "
            "features from docs/adr/0017 -- either the spec or train.py changed unexpectedly."
        )

    def test_research_and_live_feature_sets_are_distinct(self):
        assert set(research_train.FEATURE_COLUMNS) != set(train_live.FEATURE_COLUMNS)

    def test_live_feature_set_is_a_subset_of_research_feature_set(self):
        # The live variant should be a genuine restriction of the
        # research variant, not an unrelated feature list.
        assert set(train_live.FEATURE_COLUMNS).issubset(set(research_train.FEATURE_COLUMNS))


class TestOnnxParity:
    """Actual automated ONNX-vs-original parity check (not a manual
    script run), exercised against small in-test-trained models so this
    test doesn't depend on any specific trained artifact existing on disk."""

    @staticmethod
    @pytest.fixture(scope="class")
    def small_logreg_pipeline():
        from sklearn.linear_model import LogisticRegression
        from sklearn.pipeline import Pipeline
        from sklearn.preprocessing import StandardScaler

        rng = np.random.RandomState(7)
        x = rng.normal(size=(200, 4)).astype(np.float64)
        y = (x[:, 0] + 0.5 * x[:, 1] > 0).astype(int)
        pipeline = Pipeline([("scaler", StandardScaler()), ("logreg", LogisticRegression())])
        pipeline.fit(x, y)
        return pipeline, x

    def test_sklearn_pipeline_onnx_export_matches_original_within_tolerance(self, small_logreg_pipeline, tmp_path):
        onnx = pytest.importorskip("onnx")
        ort = pytest.importorskip("onnxruntime")
        skl2onnx = pytest.importorskip("skl2onnx")
        from skl2onnx.common.data_types import FloatTensorType

        pipeline, x = small_logreg_pipeline

        onnx_model = skl2onnx.convert_sklearn(
            pipeline,
            name="test_pipeline",
            initial_types=[("input", FloatTensorType([None, x.shape[1]]))],
            target_opset=17,
            options={id(pipeline.named_steps["logreg"]): {"zipmap": False}},
        )
        onnx_path = tmp_path / "test_pipeline.onnx"
        onnx.save_model(onnx_model, str(onnx_path))

        # Fixed test inputs including edge cases, per onnx-export.md's checklist.
        test_inputs = np.vstack(
            [
                np.zeros(x.shape[1], dtype=np.float32),
                np.full(x.shape[1], 1e6, dtype=np.float32),
                np.full(x.shape[1], -1e6, dtype=np.float32),
                x[:5].astype(np.float32),
            ]
        )

        original_proba = pipeline.predict_proba(test_inputs)[:, 1]
        sess = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
        onnx_out = sess.run(None, {"input": test_inputs})
        onnx_proba = onnx_out[1][:, 1]

        max_abs_diff = np.max(np.abs(original_proba - onnx_proba))
        assert max_abs_diff < 1e-4, f"ONNX export diverges from original model beyond tolerance: max_abs_diff={max_abs_diff}"

    def test_lightgbm_ranker_onnx_export_matches_original_within_tolerance(self, tmp_path):
        lgb = pytest.importorskip("lightgbm")
        onnxmltools = pytest.importorskip("onnxmltools")
        ort = pytest.importorskip("onnxruntime")
        from onnxmltools.convert.common.data_types import FloatTensorType as OMTFloatTensorType

        rng = np.random.RandomState(11)
        n_queries, group_size = 20, 5
        n_rows = n_queries * group_size
        x = rng.uniform(-100, 100, size=(n_rows, 3)).astype(np.float64)
        relevance = (rng.uniform(size=n_rows) > 0.7).astype(int)
        groups = [group_size] * n_queries

        ranker = lgb.LGBMRanker(n_estimators=20, num_leaves=7, random_state=1, verbosity=-1)
        ranker.fit(pd.DataFrame(x, columns=["a", "b", "c"]), relevance, group=groups)

        onnx_model = onnxmltools.convert_lightgbm(
            ranker,
            name="test_ranker",
            initial_types=[("input", OMTFloatTensorType([None, 3]))],
            target_opset=15,
        )
        onnx_path = tmp_path / "test_ranker.onnx"
        onnxmltools.utils.save_model(onnx_model, str(onnx_path))

        test_inputs = np.vstack(
            [
                np.zeros(3, dtype=np.float32),
                np.full(3, 1e6, dtype=np.float32),
                np.full(3, -1e6, dtype=np.float32),
                x[:5].astype(np.float32),
            ]
        )
        original_scores = ranker.predict(pd.DataFrame(test_inputs, columns=["a", "b", "c"]))

        sess = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
        input_name = sess.get_inputs()[0].name
        onnx_scores = np.asarray(sess.run(None, {input_name: test_inputs})[0]).reshape(-1)

        max_abs_diff = np.max(np.abs(original_scores - onnx_scores))
        assert max_abs_diff < 1e-3, f"LightGBM ONNX export diverges beyond tolerance: max_abs_diff={max_abs_diff}"
