import logging
import whisper

from config import Config

logger = logging.getLogger(__name__)


class Transcriber:
    """Whisper-based video/audio transcription (Ukrainian)."""

    def __init__(self, config: Config) -> None:
        self._model_name = config.whisper_model
        self._model = None  # lazy-loaded

    def _load_model(self):
        if self._model is None:
            logger.info("Loading Whisper model: %s", self._model_name)
            self._model = whisper.load_model(self._model_name)
        return self._model

    def transcribe(self, video_path: str) -> list[dict]:
        """Transcribe a video file and return timestamped segments.

        Each segment: {"start": float, "end": float, "text": str}
        """
        logger.info("Transcribing: %s", video_path)
        model = self._load_model()
        result = model.transcribe(video_path, language="uk")
        segments = result["segments"]
        logger.info("Transcription complete: %d segments", len(segments))
        return segments
