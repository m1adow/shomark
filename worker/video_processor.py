import os
import json
import logging
import subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed

logger = logging.getLogger(__name__)


class VideoProcessor:
    """Cut and reframe highlight clips into a 9:16 blur-background canvas.

    Each clip is produced in two passes:
    1. A fast lossless cut (``-c copy``) isolates the highlight segment.
    2. An FFmpeg ``filter_complex`` composites the segment onto a 9:16 canvas:
       the 16:9 source is scaled up and blurred to fill the background, and the
       original 16:9 video is overlaid centered at full width.
    """

    def __init__(self, output_dir: str = "/tmp/highlights", encoder: str = "libx264") -> None:
        self._output_dir = output_dir
        self._encoder = encoder

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
                # Pass 2: blur-background 9:16 composite
                self._reframe_segment(segment_path, output_path, self._encoder)
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
    def _reframe_segment(input_path: str, output_path: str, encoder: str = "libx264") -> None:
        """Composite a 9:16 output: heavily blurred scaled-up background + centered 16:9 overlay.

        The input (typically 16:9) is duplicated:
        - Background: scaled to fill 1080×1920, cropped, then heavily blurred (sigma=40).
        - Foreground: scaled to 1080px wide preserving aspect ratio, overlaid centered.
        """
        filter_complex = (
            "[0:v]split=2[bg_src][fg_src];"
            "[bg_src]scale=1080:1920:force_original_aspect_ratio=increase,"
            "crop=1080:1920,gblur=sigma=40[blurred];"
            "[fg_src]scale=1080:-2[scaled];"
            "[blurred][scaled]overlay=(W-w)/2:(H-h)/2[out]"
        )
        vcodec_args = (
            ["-preset", "p4", "-cq", "23"]
            if encoder == "h264_nvenc"
            else ["-preset", "fast", "-crf", "23"]
        )
        subprocess.run(
            [
                "ffmpeg", "-y",
                "-i", input_path,
                "-filter_complex", filter_complex,
                "-map", "[out]",
                "-map", "0:a?",
                "-c:v", encoder,
                *vcodec_args,
                "-c:a", "aac",
                "-b:a", "128k",
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
