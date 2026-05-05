#!/usr/bin/env python3
"""Worker performance benchmark — measures Whisper and LLM stage timing on a local video.

Usage:
    python benchmark.py <video_path> [options]

Examples:
    # Two runs: first cold (miss), second warm (cache hit)
    python benchmark.py /path/to/event.mp4

    # Single cold run, Professionals audience, with context
    python benchmark.py /path/to/event.mp4 --audience Professionals --runs 1 \\
        --description "IT workshop on cloud architecture"

Options:
    --audience     Applicants | Masters | Professionals  (default: Applicants)
    --runs         Number of pipeline runs               (default: 2)
    --description  Optional campaign context passed to the LLM

The first run is always a "cold" run: Whisper transcribes the video and writes
the result to the MinIO cache bucket.  Subsequent runs hit the cache and skip
Whisper, exercising only the LLM highlight-detection stage.

Per-step timings for hash, cache_lookup, whisper, cache_write, and llm_total
are printed after each run, plus a cache speedup ratio when --runs >= 2.
"""

import argparse
import hashlib
import logging
import sys
import time

from config import Config
from highlight_finder import HighlightFinder
from llm import LLMClient
from storage import StorageClient
from transcriber import Transcriber

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("benchmark")


# ---------------------------------------------------------------------------
# Cache key (mirrors service._build_cache_key)
# ---------------------------------------------------------------------------

def _build_cache_key(local_path: str, config: Config, storage: StorageClient) -> str:
    content_hash = storage.compute_content_hash(local_path)
    combined = (
        f"{content_hash}:{config.whisper_model}:{config.whisper_device}"
        f":{config.whisper_compute_type}:{config.whisper_beam_size}"
    )
    return "transcripts/" + hashlib.sha256(combined.encode()).hexdigest() + ".json"


# ---------------------------------------------------------------------------
# Single pipeline run (transcribe + highlight detection only)
# ---------------------------------------------------------------------------

def run_once(
    video_path: str,
    audience: str,
    description: str | None,
    config: Config,
    storage: StorageClient,
    transcriber: Transcriber,
    highlight_finder: HighlightFinder,
) -> dict[str, float]:
    """Run the Whisper+LLM pipeline stages and return a timing dict."""
    timings: dict[str, float] = {}
    t_start = time.perf_counter()

    # --- Content hash (required for cache key) ---
    t0 = time.perf_counter()
    cache_key = _build_cache_key(video_path, config, storage)
    timings["hash"] = time.perf_counter() - t0

    # --- Transcript cache lookup ---
    t0 = time.perf_counter()
    segments = None
    if config.cache_enabled:
        segments = storage.load_transcript_cache(config.cache_bucket, cache_key)
    timings["cache_lookup"] = time.perf_counter() - t0

    cache_hit = segments is not None
    timings["_cache_hit"] = 1.0 if cache_hit else 0.0

    if not cache_hit:
        # --- Whisper transcription ---
        t0 = time.perf_counter()
        segments = transcriber.transcribe(video_path)
        timings["whisper"] = time.perf_counter() - t0

        # --- Cache write ---
        if config.cache_enabled and segments:
            t0 = time.perf_counter()
            storage.save_transcript_cache(config.cache_bucket, cache_key, segments)
            timings["cache_write"] = time.perf_counter() - t0

    # --- LLM highlight detection ---
    t0 = time.perf_counter()
    highlights = highlight_finder.find_highlights(
        segments,
        target_audience=audience,
        description=description,
    )
    timings["llm_total"] = time.perf_counter() - t0

    timings["_total"] = time.perf_counter() - t_start
    timings["_highlight_count"] = float(len(highlights))
    return timings


# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------

def print_run_report(run_idx: int, timings: dict[str, float]) -> None:
    cache_hit = bool(timings.get("_cache_hit", 0))
    total = timings["_total"]
    label = "WARM (cache hit)" if cache_hit else "COLD (cache miss)"
    print(f"\n  Run {run_idx}: {label} — total {total:.1f}s")
    for key, val in timings.items():
        if key.startswith("_"):
            continue
        print(f"    {key:<20} {val:.2f}s")
    print(f"    {'highlights found':<20} {int(timings.get('_highlight_count', 0))}")


def print_summary(results: list[dict]) -> None:
    if len(results) < 2:
        return
    cold = results[0]["_total"]
    warm = min(r["_total"] for r in results[1:])
    speedup = cold / warm if warm > 0 else float("inf")
    print(f"\n  Cache speedup:  {speedup:.1f}x  (cold={cold:.1f}s → warm={warm:.1f}s)")


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main() -> None:
    parser = argparse.ArgumentParser(
        description="ShoMark worker benchmark (Whisper + LLM stages)",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("video", help="Local path to source video file")
    parser.add_argument(
        "--audience", default="Applicants",
        choices=["Applicants", "Masters", "Professionals"],
    )
    parser.add_argument(
        "--runs", type=int, default=2,
        help="Number of runs (run 1=cold, run 2+=warm with transcript cache)",
    )
    parser.add_argument("--description", default=None)
    args = parser.parse_args()

    config = Config()
    storage = StorageClient(config)
    llm = LLMClient(config)
    transcriber = Transcriber(config)
    highlight_finder = HighlightFinder(llm, config)

    sep = "=" * 60
    print(f"\n{sep}")
    print("  ShoMark Worker Benchmark")
    print(f"  Video:         {args.video}")
    print(f"  Audience:      {args.audience}")
    print(f"  Whisper:       {config.whisper_model} / {config.whisper_device} / {config.whisper_compute_type}")
    print(f"  Batch size:    {config.whisper_batch_size if config.whisper_batch_size > 0 else 'disabled'}")
    print(f"  Ollama model:  {config.ollama_model}  (num_predict={config.ollama_num_predict})")
    print(f"  Cache:         {'enabled → ' + config.cache_bucket if config.cache_enabled else 'disabled'}")
    print(f"  Runs:          {args.runs}")
    print(sep)

    results: list[dict] = []
    for i in range(1, args.runs + 1):
        timings = run_once(
            args.video, args.audience, args.description,
            config, storage, transcriber, highlight_finder,
        )
        results.append(timings)
        print_run_report(i, timings)

    print_summary(results)
    print()


if __name__ == "__main__":
    main()
