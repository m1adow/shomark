import os
import json
import logging
import subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed

logger = logging.getLogger(__name__)


class VideoProcessor:
    """Cut and dynamically reframe highlight clips from a source video.

    Each clip is produced in two passes:
    1. A fast lossless cut (``-c copy``) isolates the highlight segment.
    2. ``Reframer`` analyses per-frame saliency to build a smooth 9:16 crop
       trajectory, then re-encodes with FFmpeg's ``sendcmd`` filter.
    """

    def __init__(self, output_dir: str = "/tmp/highlights", reframer=None) -> None:
        self._output_dir = output_dir
        self._reframer = reframer

    def cut_highlights(self, video_path: str, highlights: list[dict], output_dir: str | None = None) -> list[dict]:
        """Cut clips and return a list of {"path": ..., "title": ..., "reason": ...}.

        Skips clips shorter than 10 seconds or with invalid timing.
        ``output_dir`` overrides the instance-level default, allowing concurrent
        jobs to write to isolated directories without collisions.
        """
        effective_dir = output_dir or self._output_dir
        os.makedirs(effective_dir, exist_ok=True)
        duration = self._get_duration(video_path)

        def _process_clip(i: int, clip_info: dict) -> dict | None:
            start = clip_info["start"]
            end = min(clip_info["end"], duration)
            if end - start < 10:
                logger.warning("Skipping clip %d: too short (%.1fs)", i + 1, end - start)
                return None
            output_path = os.path.join(effective_dir, f"highlight_{i + 1}.mp4")
            preview_path = os.path.join(effective_dir, f"highlight_{i + 1}_preview.jpg")
            logger.info("Cutting clip %d: %.1fs -> %.1fs", i + 1, start, end)

            # Pass 1: lossless cut to an isolated segment
            segment_path = output_path + ".seg.mp4"
            self._cut_segment(video_path, start, end, segment_path)
            try:
                # Pass 2: saliency-based reframing → final 9:16 output
                trajectory = self._reframer.compute_trajectory(segment_path)
                self._reframer.render(segment_path, trajectory, output_path)
            finally:
                if os.path.exists(segment_path):
                    os.remove(segment_path)

            # Preview from the reframed output (reflects the actual crop)
            preview_offset = (end - start) * 0.25
            self._extract_frame(output_path, preview_offset, preview_path)
            return {
                "path": output_path,
                "preview_path": preview_path,
                "title": clip_info.get("title", ""),
                "reason": clip_info.get("reason", ""),
                "viral_score": clip_info.get("viral_score"),
                "hashtags": clip_info.get("hashtags"),
                "start": start,
                "end": end,
            }

        results: list[dict] = []
        with ThreadPoolExecutor(max_workers=len(highlights) or 1) as pool:
            futures = {pool.submit(_process_clip, i, clip): i for i, clip in enumerate(highlights)}
            for future in as_completed(futures):
                result = future.result()
                if result is not None:
                    results.append(result)

        results.sort(key=lambda r: r["start"])
        logger.info("Cut %d highlight clips", len(results))
        return results

    @staticmethod
    def _get_duration(video_path: str) -> float:
        """Get video duration in seconds via ffprobe."""
        result = subprocess.run(
            [
                "ffprobe", "-v", "quiet",
                "-print_format", "json",
                "-show_format",
                video_path,
            ],
            capture_output=True, text=True, check=True,
        )
        info = json.loads(result.stdout)
        return float(info["format"]["duration"])

    @staticmethod
    def _cut_segment(video_path: str, start: float, end: float, output_path: str) -> None:
        """Lossless cut of [start, end] from *video_path* into *output_path*.

        Uses stream-copy (``-c copy``) so this is near-instant and preserves
        the original encoding for the subsequent reframing pass.
        """
        subprocess.run(
            [
                "ffmpeg", "-y",
                "-ss", f"{start:.3f}",
                "-to", f"{end:.3f}",
                "-i", video_path,
                "-c", "copy",
                "-avoid_negative_ts", "make_zero",
                output_path,
            ],
            capture_output=True, check=True,
        )

    @staticmethod
    def _extract_frame(video_path: str, time: float, output_path: str) -> None:
        """Extract a single frame as a JPEG preview image."""
        subprocess.run(
            [
                "ffmpeg", "-y",
                "-ss", f"{time:.3f}",
                "-i", video_path,
                "-frames:v", "1",
                "-q:v", "2",
                output_path,
            ],
            capture_output=True, check=True,
        )
