"""Export the item-ranker's Bayesian-shrunk popularity lookup tables
(tables['global'|'hero'|'hero_minute'] in model.pkl, per item_ranker.py's
compute_item_popularity_tables) to a JSON sidecar Ml.Inference/Feature
Engine can load without a Python interop bridge. Closes ADR 0023
Decision 5's flagged gap.

Verified actual pickled shape directly (item_ranker.py's
compute_item_popularity_tables, read in full before writing this):
- tables["global"]: dict[str item_name -> float], flat.
- tables["hero"]: dict[tuple[int hero_id, str item_name] -> float].
- tables["hero_minute"]: dict[tuple[int hero_id, str minute_bucket,
  str item_name] -> float], minute_bucket in {"early", "mid", "late"}.

Tuple keys are not valid JSON object keys, so the JSON shape re-nests
each table as nested dicts keyed by stringified components instead of
flattening to a "hero_id:item" string key -- this keeps the JSON
directly walkable (table["hero"]["121"]["item_name"]) rather than
requiring string-splitting on the consumer side, and hero_id/minute
bucket are naturally string dict keys once nested (JSON object keys are
always strings; hero_id is coerced to its string form, matched back to
int on the C# side when parsing).

Run: .venv/Scripts/python.exe scripts/export_popularity_table.py
"""

from __future__ import annotations

import json
import pickle
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

from dota_training.config import MODELS_DIR  # noqa: E402

ITEM_RANKER_MODEL_ID = "item-ranker-v1.0-20260810T203622Z"


def _nest_global(global_table: dict[str, float]) -> dict[str, float]:
    return {item: float(v) for item, v in global_table.items()}


def _nest_hero(hero_table: dict[tuple[int, str], float]) -> dict[str, dict[str, float]]:
    out: dict[str, dict[str, float]] = {}
    for (hero_id, item), v in hero_table.items():
        out.setdefault(str(hero_id), {})[item] = float(v)
    return out


def _nest_hero_minute(hero_minute_table: dict[tuple[int, str, str], float]) -> dict[str, dict[str, dict[str, float]]]:
    out: dict[str, dict[str, dict[str, float]]] = {}
    for (hero_id, bucket, item), v in hero_minute_table.items():
        out.setdefault(str(hero_id), {}).setdefault(bucket, {})[item] = float(v)
    return out


def main() -> None:
    model_dir = MODELS_DIR / ITEM_RANKER_MODEL_ID
    with open(model_dir / "model.pkl", "rb") as f:
        bundle = pickle.load(f)

    tables = bundle["tables"]
    all_items = bundle["all_items"]

    # Verify the actual pickled shape live rather than trusting this
    # docstring's own claim, per the project's live-verification discipline.
    assert isinstance(tables["global"], dict)
    sample_global_key = next(iter(tables["global"]))
    assert isinstance(sample_global_key, str), f"expected global table keyed by item name (str), got {type(sample_global_key)}"

    sample_hero_key = next(iter(tables["hero"]))
    assert isinstance(sample_hero_key, tuple) and len(sample_hero_key) == 2, f"expected hero table keyed by (hero_id, item) tuple, got {sample_hero_key!r}"
    assert isinstance(sample_hero_key[0], int) and isinstance(sample_hero_key[1], str)

    sample_hm_key = next(iter(tables["hero_minute"]))
    assert isinstance(sample_hm_key, tuple) and len(sample_hm_key) == 3, f"expected hero_minute table keyed by (hero_id, bucket, item) tuple, got {sample_hm_key!r}"
    assert isinstance(sample_hm_key[0], int) and isinstance(sample_hm_key[1], str) and isinstance(sample_hm_key[2], str)

    output = {
        "metadata": {
            "source_model_id": ITEM_RANKER_MODEL_ID,
            "generated_at": datetime.now(timezone.utc).isoformat(),
            "n_items": len(all_items),
            "n_global_entries": len(tables["global"]),
            "n_hero_entries": len(tables["hero"]),
            "n_hero_minute_entries": len(tables["hero_minute"]),
            "hero_item_prior_strength": 20.0,
            "hero_minute_bucket_prior_strength": 15.0,
            "minute_buckets": ["early", "mid", "late"],
            "minute_bucket_boundaries": "early: minute < 15, mid: 15 <= minute < 30, late: minute >= 30 (item_ranker.py's _minute_bucket)",
            "shape_note": "global: {item_name: popularity}. hero: {hero_id_str: {item_name: popularity}} (originally keyed by (hero_id:int, item:str) tuple in model.pkl -- re-nested here since JSON object keys must be strings). hero_minute: {hero_id_str: {minute_bucket: {item_name: popularity}}} (originally (hero_id:int, minute_bucket:str, item:str) tuple). All three tables are Bayesian-shrunk toward their parent (hero shrinks toward global, hero_minute shrinks toward hero), per item_ranker.py's compute_item_popularity_tables -- see docs/adr/0020 Decision 5.",
        },
        "all_items": all_items,
        "global": _nest_global(tables["global"]),
        "hero": _nest_hero(tables["hero"]),
        "hero_minute": _nest_hero_minute(tables["hero_minute"]),
    }

    out_path = model_dir / "hero_item_popularity.json"
    out_path.write_text(json.dumps(output, indent=2), encoding="utf-8")
    size_bytes = out_path.stat().st_size
    print(f"wrote {out_path} ({size_bytes:,} bytes / {size_bytes / 1024:.1f} KB)")
    print(f"global: {len(tables['global'])} entries, hero: {len(tables['hero'])} entries, hero_minute: {len(tables['hero_minute'])} entries")


if __name__ == "__main__":
    main()
