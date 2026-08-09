"""Single source of truth for paths, seeds, and thresholds — see docs/adr/0017."""

from __future__ import annotations

from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[2]
DATA_DIR = PROJECT_ROOT / "data"
RAW_DIR = DATA_DIR / "raw"
BRONZE_PATH = DATA_DIR / "bronze.parquet"
NORMALIZED_PATH = DATA_DIR / "normalized.parquet"
FEATURES_PATH = DATA_DIR / "features.parquet"
DATASETS_DIR = DATA_DIR / "datasets"
MODELS_DIR = PROJECT_ROOT / "models"
FEATURES_SPEC_PATH = Path(__file__).resolve().parent / "features_spec.yaml"

OPENDOTA_BASE_URL = "https://api.opendota.com/api"
# OpenDota returns 403 without a User-Agent header — verified live.
USER_AGENT = "dota-ai-coach-training/0.1 (+https://github.com/)"
REQUESTS_PER_MINUTE = 30  # conservative: a 429 was observed live at 55/min unauthenticated; do not trust response headers, they are absent on unauthenticated calls (verified live)

# Ranked All Draft, per https://api.opendota.com/api/constants/game_mode
# verified live: {"22": "game_mode_all_draft", "23": "game_mode_turbo"}.
# This corrects an earlier, wrong assumption (22=Turbo) that would have
# silently mixed Turbo into a "ranked" dataset — see docs/adr/0017.
RANKED_GAME_MODE = 22
RANKED_LOBBY_TYPE = 7
MIN_DURATION_SECONDS = 900
MAX_DURATION_SECONDS = 4500
MAX_LEAVER_STATUS = 1  # 0=none, 1=disconnected-but-completed; >=2 is treated as abandoned

TARGET_MATCH_COUNT = 2000

TIMESTAMPS_MINUTES = [5, 10, 15, 20, 25, 30, 35, 40]
MIN_MINUTES_BEFORE_MATCH_END = 2  # drop rows within this many minutes of the final buzzer

SPLIT_SHARES = {"train": 0.60, "val": 0.15, "calib": 0.10, "test": 0.15}

RANDOM_SEED = 42

# High-impact items whose early purchase timing is a meaningful signal —
# a small, hand-curated whitelist (same "PATCH: verify" placeholder
# discipline as ItemCatalog.cs in the C# RecommendationEngines project).
KEY_ITEM_KEYS = [
    "black_king_bar",
    "blink",
    "aghanims_scepter",
    "radiance",
    "desolator",
    "battle_fury",
    "manta",
    "sheepstick",
]

CALIBRATION_BINS = 10
ECE_ISSUE_THRESHOLD = 0.05
ABLATION_FEATURE = "gold_adv_at_T"
MIN_SEGMENT_ROWS = 100
