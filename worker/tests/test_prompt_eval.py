"""Prompt-quality eval harness for HighlightFinder.

This is an integration test: it requires a running Ollama instance reachable at
``OLLAMA_URL``. When ``OLLAMA_URL`` is not set, every fixture is skipped — the
harness stays out of normal CI runs and is only invoked deliberately, for
example via:

    OLLAMA_URL=http://localhost:11434/api/generate \
    OLLAMA_MODEL=qwen2.5:7b-instruct \
    KAFKA_BOOTSTRAP_SERVERS=dummy \
    MINIO_ENDPOINT=dummy MINIO_ACCESS_KEY=dummy MINIO_SECRET_KEY=dummy \
    pytest worker/tests/test_prompt_eval.py -v -s

Each fixture in ``worker/tests/prompts/*.json`` is loaded, fed through
``HighlightFinder.find_highlights``, and scored against the per-fixture
``expected`` block. The harness reports per-metric pass/fail so prompt edits
can be A/B'd by re-running the same command against two branches.
"""
from __future__ import annotations

import json
import os
import re
import statistics
import sys
from pathlib import Path

import pytest

# Make worker/ importable when invoked from repo root.
WORKER_DIR = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(WORKER_DIR))

FIXTURES_DIR = Path(__file__).resolve().parent / "prompts"

# Skip the entire module unless a real Ollama is configured.
pytestmark = pytest.mark.skipif(
    not os.getenv("OLLAMA_URL"),
    reason="OLLAMA_URL not set — eval harness requires a running Ollama instance.",
)

GENERIC_TITLE_REGEX = re.compile(
    r"^\s*(цікав|захопл|важлив|корисн|ключов)",
    re.IGNORECASE,
)


def _load_fixtures() -> list[tuple[str, dict]]:
    fixtures: list[tuple[str, dict]] = []
    for path in sorted(FIXTURES_DIR.glob("*.json")):
        with path.open(encoding="utf-8") as fh:
            fixtures.append((path.stem, json.load(fh)))
    return fixtures


def _score_clip_set(clips: list[dict], expected: dict, segments: list[dict]) -> dict:
    """Return a dict of metric_name → (passed: bool, detail: str)."""
    metrics: dict[str, tuple[bool, str]] = {}

    # Count window
    n = len(clips)
    lo = expected.get("min_count", 1)
    hi = expected.get("max_count", 99)
    metrics["count_in_window"] = (lo <= n <= hi, f"got {n}, want [{lo},{hi}]")

    # No clip starts below floor
    floor = expected.get("forbid_starts_below", 0.0)
    bad_low = [c for c in clips if float(c.get("start", 0)) < floor]
    metrics["no_early_starts"] = (not bad_low, f"{len(bad_low)} clip(s) below {floor}s")

    # Starts grounded to a real segment within ±2 s
    seg_starts = [s["start"] for s in segments]
    ungrounded = [
        c for c in clips
        if not any(abs(float(c.get("start", -999)) - s) <= 2.0 for s in seg_starts)
    ]
    metrics["starts_grounded"] = (
        not ungrounded,
        f"{len(ungrounded)} clip(s) not grounded to a segment ±2s",
    )

    # Min pairwise start gap
    gap_floor = expected.get("min_pairwise_start_gap", 0.0)
    starts = sorted(float(c.get("start", 0)) for c in clips)
    too_close = [
        (a, b) for a, b in zip(starts, starts[1:]) if (b - a) < gap_floor
    ]
    metrics["pairwise_gap_ok"] = (
        not too_close,
        f"{len(too_close)} pair(s) closer than {gap_floor}s",
    )

    # No overlapping intervals
    overlapping = []
    sorted_clips = sorted(clips, key=lambda c: float(c.get("start", 0)))
    for a, b in zip(sorted_clips, sorted_clips[1:]):
        if float(a.get("end", 0)) > float(b.get("start", 0)):
            overlapping.append((a.get("start"), b.get("start")))
    metrics["no_overlap"] = (
        not overlapping,
        f"{len(overlapping)} overlapping pair(s)",
    )

    # Title boilerplate ban
    bad_titles = [c.get("title", "") for c in clips if GENERIC_TITLE_REGEX.search(c.get("title", ""))]
    custom_ban = expected.get("title_must_not_match_regex")
    if custom_ban:
        custom_re = re.compile(custom_ban, re.IGNORECASE)
        bad_titles += [c.get("title", "") for c in clips if custom_re.search(c.get("title", ""))]
    metrics["titles_concrete"] = (not bad_titles, f"{len(bad_titles)} generic title(s): {bad_titles[:3]}")

    # Title must reference at least one expected entity (any clip is enough)
    keywords = expected.get("title_must_reference_one_of")
    if keywords:
        any_match = any(
            any(k.lower() in (c.get("title", "") + " " + c.get("reason", "")).lower() for k in keywords)
            for c in clips
        )
        metrics["titles_reference_entity"] = (
            any_match,
            f"no clip referenced any of {keywords}",
        )

    # Per-clip 1:1 with expected entity list (used by user-instruction fixtures)
    per_clip_keywords = expected.get("title_must_match_one_per_clip")
    if per_clip_keywords:
        # Each keyword must appear in at least one distinct clip's title or reason.
        matched = set()
        for c in clips:
            blob = (c.get("title", "") + " " + c.get("reason", "")).lower()
            for k in per_clip_keywords:
                if k.lower() in blob and k not in matched:
                    matched.add(k)
                    break
        missing = [k for k in per_clip_keywords if k not in matched]
        metrics["entity_coverage"] = (
            not missing,
            f"missing entities: {missing}",
        )

    # viral_score spread
    floor_stdev = expected.get("viral_score_min_stdev", 0.0)
    if floor_stdev > 0 and len(clips) >= 2:
        scores = [float(c.get("viral_score", 0)) for c in clips]
        actual_stdev = statistics.pstdev(scores) if scores else 0.0
        metrics["score_spread"] = (
            actual_stdev >= floor_stdev,
            f"stdev={actual_stdev:.3f}, want >= {floor_stdev}",
        )

    # All Cyrillic-only string fields
    cyr = re.compile(r"[\u0400-\u04FF]")
    bad_lang = []
    for c in clips:
        for field in ("title", "reason", "hashtags"):
            v = c.get(field, "") or ""
            if v and not cyr.search(v):
                bad_lang.append(f"{field}={v[:40]!r}")
    metrics["ukrainian_only"] = (not bad_lang, f"{len(bad_lang)} non-Cyrillic field(s): {bad_lang[:2]}")

    return metrics


def _print_report(name: str, clips: list[dict], metrics: dict) -> None:
    border = "=" * 80
    print(f"\n{border}\nFIXTURE: {name}\n{border}")
    for m, (passed, detail) in metrics.items():
        flag = "PASS" if passed else "FAIL"
        print(f"  [{flag}] {m:24s} — {detail}")
    print(f"  {len(clips)} clip(s):")
    for c in clips:
        print(
            f"    [{c.get('start'):>6}s..{c.get('end'):>6}s] vs={c.get('viral_score'):.2f} | "
            f"{c.get('title', '')!r}"
        )
        print(f"        reason: {c.get('reason', '')!r}")
        print(f"        tags:   {c.get('hashtags', '')!r}")


@pytest.mark.parametrize("name,fixture", _load_fixtures(), ids=lambda f: f if isinstance(f, str) else "")
def test_prompt_quality(name, fixture):
    """Run one fixture through HighlightFinder and assert all metrics pass."""
    from config import Config
    from highlight_finder import HighlightFinder
    from llm import LLMClient

    config = Config()
    finder = HighlightFinder(LLMClient(config), config)

    clips = finder.find_highlights(
        segments=fixture["segments"],
        target_audience=fixture.get("audience"),
        description=fixture.get("description"),
        additional_instructions=fixture.get("additional_instructions"),
    )
    metrics = _score_clip_set(clips, fixture["expected"], fixture["segments"])
    _print_report(name, clips, metrics)

    failed = [m for m, (passed, _) in metrics.items() if not passed]
    assert not failed, f"Failed metrics for {name}: {failed}"
