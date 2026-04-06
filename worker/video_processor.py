import os
import logging
from moviepy.editor import VideoFileClip

logger = logging.getLogger(__name__)


class VideoProcessor:
    """Cut highlight clips from a source video."""

    def __init__(self, output_dir: str = "/tmp/highlights") -> None:
        self._output_dir = output_dir

    def cut_highlights(self, video_path: str, highlights: list[dict]) -> list[dict]:
        """Cut clips and return a list of {"path": ..., "title": ..., "reason": ...}.

        Skips clips shorter than 10 seconds or with invalid timing.
        """
        os.makedirs(self._output_dir, exist_ok=True)
        video = VideoFileClip(video_path)
        results: list[dict] = []

        try:
            for i, clip_info in enumerate(highlights):
                start = clip_info["start"]
                end = min(clip_info["end"], video.duration)

                if end - start < 10:
                    logger.warning("Skipping clip %d: too short (%.1fs)", i + 1, end - start)
                    continue

                output_path = os.path.join(self._output_dir, f"highlight_{i + 1}.mp4")
                logger.info("Cutting clip %d: %.1fs -> %.1fs", i + 1, start, end)

                sub = video.subclip(start, end)
                sub.write_videofile(output_path, codec="libx264", audio_codec="aac", logger=None)

                # Generate preview image at 25% into the clip
                preview_time = start + (end - start) * 0.25
                preview_path = os.path.join(self._output_dir, f"highlight_{i + 1}_preview.jpg")
                video.save_frame(preview_path, t=preview_time)
                logger.info("Preview image saved: %s (t=%.1fs)", preview_path, preview_time)

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
        finally:
            video.close()

        logger.info("Cut %d highlight clips", len(results))
        return results
