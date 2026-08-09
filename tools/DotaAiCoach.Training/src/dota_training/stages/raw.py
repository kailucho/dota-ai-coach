"""RAW stage: reads the JSON envelopes written by opendota/client.py.
Never mutates them — only reads and yields parsed bodies for BRONZE."""

from __future__ import annotations

import json
from collections.abc import Iterator
from pathlib import Path
from typing import Any

from dota_training.config import RAW_DIR


def iter_raw_match_envelopes(raw_dir: Path = RAW_DIR) -> Iterator[dict[str, Any]]:
    """Yields every cached /matches/{id} envelope (skips /explorer / /parsedMatches
    responses, which don't represent a single match's parsed detail)."""
    for path in sorted(raw_dir.glob("_matches_*.json")):
        envelope = json.loads(path.read_text(encoding="utf-8"))
        endpoint = envelope["endpoint"]
        # /matches/{id} envelopes have no query string; /matches/{id}/... would.
        if "?" in endpoint:
            continue
        yield envelope


def parse_match_body(envelope: dict[str, Any]) -> dict[str, Any] | None:
    """Returns the parsed match JSON body, or None if malformed (BRONZE rejects these, doesn't crash)."""
    try:
        return json.loads(envelope["body"])
    except (json.JSONDecodeError, KeyError, TypeError):
        return None
