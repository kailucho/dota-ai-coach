"""Rate-limited OpenDota HTTP client, writing RAW envelopes compatible with
the C# RawResponseCache format (see src/DotaAiCoach.OpenDota/RawResponseCache.cs)."""

from __future__ import annotations

import hashlib
import json
import re
import time
from collections import deque
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import requests

from dota_training.config import OPENDOTA_BASE_URL, RAW_DIR, REQUESTS_PER_MINUTE, USER_AGENT


class RateLimiter:
    """Sliding 60s window token bucket. Does not trust response headers —
    verified live that x-rate-limit-remaining-* is absent on unauthenticated
    calls, unlike the C# client's assumption for authenticated calls."""

    def __init__(self, requests_per_minute: int = REQUESTS_PER_MINUTE):
        self._limit = requests_per_minute
        self._timestamps: deque[float] = deque()

    def wait_for_slot(self) -> None:
        while True:
            now = time.monotonic()
            while self._timestamps and now - self._timestamps[0] >= 60:
                self._timestamps.popleft()

            if len(self._timestamps) < self._limit:
                self._timestamps.append(now)
                return

            time.sleep(60 - (now - self._timestamps[0]) + 0.1)


def _cache_path(endpoint: str) -> Path:
    sanitized = re.sub(r"[^A-Za-z0-9]", "_", endpoint)
    digest = hashlib.sha256(endpoint.encode()).hexdigest()[:8]
    return RAW_DIR / f"{sanitized}_{digest}.json"


class OpenDotaClient:
    def __init__(self, raw_dir: Path = RAW_DIR, rate_limiter: RateLimiter | None = None):
        self._raw_dir = raw_dir
        self._raw_dir.mkdir(parents=True, exist_ok=True)
        self._session = requests.Session()
        self._session.headers["User-Agent"] = USER_AGENT
        self._rate_limiter = rate_limiter or RateLimiter()

    def get_json(self, endpoint: str, params: dict[str, Any] | None = None, *, force_refetch: bool = False) -> Any:
        """Returns the parsed JSON body, writing (and preferring, unless
        force_refetch) a RAW envelope on disk — making acquisition resumable."""
        cache_key = endpoint if not params else f"{endpoint}?{'&'.join(f'{k}={v}' for k, v in sorted(params.items()))}"
        path = _cache_path(cache_key)

        if not force_refetch and path.exists():
            envelope = json.loads(path.read_text(encoding="utf-8"))
            return json.loads(envelope["body"])

        response = self._get_with_retry(endpoint, params)
        body = response.text

        envelope = {
            "endpoint": cache_key,
            "fetchedAtUtc": datetime.now(timezone.utc).isoformat(),
            "statusCode": response.status_code,
            "sourceName": "opendota",
            "schemaVersion": 1,
            "body": body,
            "isParsed": False,
        }
        path.write_text(json.dumps(envelope), encoding="utf-8")
        return json.loads(body)

    def explorer_sql(self, sql: str) -> list[dict[str, Any]]:
        """GET /explorer — no on-disk caching (queries vary too much to key sensibly); still rate-limited."""
        response = self._get_with_retry("/explorer", {"sql": sql})
        return response.json().get("rows", [])

    def _get_with_retry(self, endpoint: str, params: dict[str, Any] | None, max_attempts: int = 5) -> requests.Response:
        """Retries on 429 with exponential backoff — the observed real-world
        signal that our local token bucket is still too optimistic relative
        to OpenDota's actual (unauthenticated, headerless) limit.

        Also retries on requests.exceptions.RequestException (connection
        errors, read timeouts, etc.) — found live during the ADR-0021
        multi-patch acquisition run: an unhandled requests.exceptions.ReadTimeout
        (a transient network issue, not a 429) crashed the whole run and lost
        in-flight progress on that single match. A single flaky network call
        must not kill a run that's already fetched thousands of matches --
        same principle acquire.py's fetch_parsed_matches already applies at
        the per-match level, but the low-level HTTP retry itself was not yet
        hardened against non-429 transient failures."""
        backoff = 5.0
        for attempt in range(max_attempts):
            self._rate_limiter.wait_for_slot()
            try:
                response = self._session.get(f"{OPENDOTA_BASE_URL}{endpoint}", params=params, timeout=30)
            except requests.exceptions.RequestException as exc:
                if attempt == max_attempts - 1:
                    raise
                time.sleep(backoff)
                backoff *= 2
                continue
            if response.status_code != 429:
                response.raise_for_status()
                return response
            if attempt == max_attempts - 1:
                response.raise_for_status()
            time.sleep(backoff)
            backoff *= 2
        raise RuntimeError("unreachable")  # pragma: no cover
