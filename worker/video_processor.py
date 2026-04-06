import os
import json
import logging
import subprocess

logger = logging.getLogger(__name__)


class VideoProcessor:
    """Cut highlight clips from a source video using FFmpeg."""

    def __init__(self, output_dir: str = "/tmp/highlights") -> None:
        self._output_dir = output_dir

    def cut_highlights(self, video_path: str, highlights: list[dict]) -> list[dict]:
        """Cut clips and return a list of {"path": ..., "title": ..., "reason": ...}.

        Skips clips shorter than 10 seconds or with invalid timing.
        """
        os.makedirs(self._output_dir, exist_ok=True)
        duration = self._get_duration(video_path)
        results: list[dict] = []

        for i, clip_info in enumerate(highlights):
            start = clip_info["start"]
            end = min(clip_info["end"], duration)

            if end - start < 10:
                logger.warning("Skipping clip %d: too short (%.1fs)", i + 1, end - start)
                continue

            output_path = os.path.join(self._output_dir, f"highlight_{i + 1}.mp4")
            preview_path = os.path.join(self._output_dir, f"highlight_{i + 1}_preview.jpg")
            logger.info("Cutting clip %d: %.1fs -> %.1fs", i + 1, start, end)

            # 1. Cut and encode to 9:16 vertical format for Reels/TikTok/Shorts
            self._cut_vertical(video_path, start, end, output_path)

            # 2. Extract preview frame at 25% into the clip
            preview_time = start + (end - start) * 0.25
            self._extract_frame(video_path, preview_time, preview_path)

            results.append({
                "path": output_path,
                "preview_path": preview_path,
                "title": clip_info.get("title", ""),
                "reason": clip_info.get("reason", ""),
                "viral_score": clip_info.get("viral_score"),
                "hashtags": clip_info.get("hashtags"),
                "start": start,
                "end": end,
            })

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
    def _cut_vertical(video_path: str, start: float, end: float, output_path: str) -> None:
        """Cut a segment and encode to 9:16 (1080x1920) optimized for TikTok/Reels/Shorts."""
        subprocess.run(
            [
                "ffmpeg", "-y",
                "-ss", f"{start:.3f}",
                "-to", f"{end:.3f}",
                "-i", video_path,
                "-vf", (
                    "crop=ih*9/16:ih,"      # center-crop to 9:16 aspect ratio
                    "scale=1080:1920,"       # scale to 1080x1920 (upscale if needed)
                    "setsar=1"               # square pixels
                ),
                "-r", "30",                          # 30 fps (platform standard)
                "-c:v", "libx264",
                "-profile:v", "high",                # H.264 High profile
                "-level:v", "4.0",                   # max device compatibility
                "-preset", "fast",
                "-crf", "23",
                "-pix_fmt", "yuv420p",               # required for mobile playback
                "-c:a", "aac", "-b:a", "128k",
                "-ar", "44100",                      # 44.1 kHz audio (platform standard)
                "-movflags", "+faststart",           # moov atom at start for instant playback
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
