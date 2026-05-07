import hashlib
import json
import os
import logging
import shutil
import time
import uuid
from concurrent.futures import ThreadPoolExecutor

from config import Config
from storage import StorageClient
from transcriber import Transcriber
from highlight_finder import HighlightFinder
from video_processor import VideoProcessor

logger = logging.getLogger(__name__)


class VideoHighlightService:
    """Orchestrates the full video-to-highlights pipeline.

    Steps:
      1. Download video from MinIO
      2. Transcribe with Whisper
      3. Find highlights via LLM map-reduce
      4. Cut video clips
      5. Upload highlights to MinIO
    """

    def __init__(
        self,
        storage: StorageClient,
        transcriber: Transcriber,
        highlight_finder: HighlightFinder,
        video_processor: VideoProcessor,
        config: Config,
    ) -> None:
        self._storage = storage
        self._transcriber = transcriber
        self._highlight_finder = highlight_finder
        self._video_processor = video_processor
        self._cache_bucket = config.cache_bucket
        self._cache_enabled = config.cache_enabled
        self._whisper_fingerprint = (
            f"{config.whisper_model}:{config.whisper_device}"
            f":{config.whisper_compute_type}:{config.whisper_beam_size}"
        )

    def process(self, message: dict) -> dict | None:
        """Process a single Kafka message. Returns a completion result or None.

        Expected message format:
        {
            "video_bucket": "videos",
            "video_key": "path/to/video.mp4",
            "output_bucket": "highlights",
            "output_prefix": "event-123/"
        }
        """
        video_bucket = message["video_bucket"]
        video_key = message["video_key"]
        output_bucket = message.get("output_bucket", "highlights")
        output_prefix = message.get("output_prefix", "")
        target_audience = message.get("target_audience")
        description = message.get("description")
        additional_instructions = message.get("additional_instructions")
        exclude_ranges: list[dict] | None = message.get("exclude_ranges") or None

        work_dir = f"/tmp/work/{uuid.uuid4().hex}"
        local_video = os.path.join(work_dir, "source.mp4")

        timings: dict[str, float] = {}
        t_start = time.perf_counter()

        try:
            # 1. Download
            logger.info("=== Step 1/5: Downloading video ===")
            t0 = time.perf_counter()
            self._storage.download_video(video_bucket, video_key, local_video)
            timings["download"] = time.perf_counter() - t0

            # 2. Transcribe (cache-aware)
            logger.info("=== Step 2/5: Transcribing ===")
            t0 = time.perf_counter()
            cache_key: str | None = None
            segments: list[dict] | None = None
            if self._cache_enabled:
                cache_key = self._build_cache_key(local_video)
                segments = self._storage.load_transcript_cache(self._cache_bucket, cache_key)
            if segments is not None:
                logger.info("Transcript cache HIT — Whisper skipped")
            else:
                segments = self._transcriber.transcribe(local_video)
                if self._cache_enabled and cache_key is not None:
                    self._storage.save_transcript_cache(self._cache_bucket, cache_key, segments)
            timings["transcribe"] = time.perf_counter() - t0

            # 3. Find highlights
            logger.info("=== Step 3/5: Finding highlights (LLM map-reduce) ===")
            t0 = time.perf_counter()
            highlights = self._highlight_finder.find_highlights(
                segments,
                target_audience=target_audience,
                description=description,
                additional_instructions=additional_instructions,
                exclude_ranges=exclude_ranges,
            )
            timings["llm"] = time.perf_counter() - t0
            if not highlights:
                logger.warning("No highlights found, skipping video: %s", video_key)
                return None

            # 4. Cut clips
            logger.info("=== Step 4/5: Cutting clips ===")
            t0 = time.perf_counter()
            clips = self._video_processor.cut_highlights(local_video, highlights, output_dir=os.path.join(work_dir, "highlights"))
            timings["cut"] = time.perf_counter() - t0

            # 5. Upload
            logger.info("=== Step 5/5: Uploading %d clips ===", len(clips))
            t0 = time.perf_counter()
            uploaded_clips: list[dict] = []

            with ThreadPoolExecutor(max_workers=4) as pool:
                for clip in clips:
                    clip_name = os.path.basename(clip["path"])
                    clip_key = f"{output_prefix}{clip_name}"

                    # Upload vertical (9:16) video clip
                    pool.submit(self._storage.upload_file, output_bucket, clip_key, clip["path"], "video/mp4")

                    # Upload preview image
                    preview_name = os.path.basename(clip["preview_path"])
                    preview_key = f"{output_prefix}{preview_name}"
                    pool.submit(self._storage.upload_file, output_bucket, preview_key, clip["preview_path"], "image/jpeg")

                    # Extract original transcript text for this clip's time range
                    transcript_text = self._extract_text(segments, clip["start"], clip["end"])

                    # Upload metadata
                    meta_key = f"{clip_key}.json"
                    meta_path = f"{clip['path']}.json"
                    metadata = {
                        "title": clip["title"],
                        "reason": clip["reason"],
                        "viral_score": clip.get("viral_score"),
                        "hashtags": clip.get("hashtags"),
                        "start": clip["start"],
                        "end": clip["end"],
                        "transcript": transcript_text,
                    }
                    with open(meta_path, "w", encoding="utf-8") as f:
                        json.dump(metadata, f, ensure_ascii=False, indent=2)
                    pool.submit(self._storage.upload_file, output_bucket, meta_key, meta_path, "application/json")

                    uploaded_clips.append({
                        "key": clip_key,
                        "preview_key": preview_key,
                        "meta_key": meta_key,
                        "title": clip["title"],
                        "viral_score": clip.get("viral_score"),
                        "hashtags": clip.get("hashtags"),
                        "start": clip["start"],
                        "end": clip["end"],
                    })

            timings["upload"] = time.perf_counter() - t0
            total = time.perf_counter() - t_start
            logger.info(
                "=== TIMING [%s]: %s | total=%.1fs ===",
                video_key,
                " ".join(f"{k}={v:.1f}s" for k, v in timings.items()),
                total,
            )
            logger.info("=== Done: %s -> %d highlights uploaded ===", video_key, len(clips))

            return {
                "video_bucket": video_bucket,
                "video_key": video_key,
                "output_bucket": output_bucket,
                "highlights": uploaded_clips,
            }

        finally:
            # Cleanup temporary files
            if os.path.exists(work_dir):
                shutil.rmtree(work_dir, ignore_errors=True)

    def _build_cache_key(self, local_video: str) -> str:
        """Derive the MinIO object key for this video's transcript cache.

        The key is a SHA-256 hash of the file's content combined with the
        active Whisper settings, so any model/device change auto-invalidates.
        """
        content_hash = self._storage.compute_content_hash(local_video)
        combined = f"{content_hash}:{self._whisper_fingerprint}"
        cache_id = hashlib.sha256(combined.encode()).hexdigest()
        return f"transcripts/{cache_id}.json"

    @staticmethod
    def _extract_text(segments: list[dict], start: float, end: float) -> str:
        """Join transcript segment texts that overlap the given time range."""
        parts = [
            seg["text"].strip()
            for seg in segments
            if seg["end"] > start and seg["start"] < end
        ]
        return " ".join(parts)
