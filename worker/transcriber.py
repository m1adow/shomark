import logging
import os
import subprocess

from faster_whisper import WhisperModel

from config import Config

logger = logging.getLogger(__name__)


class Transcriber:
    """faster-whisper based video/audio transcription (Ukrainian)."""

    def __init__(self, config: Config) -> None:
        self._model_name = config.whisper_model
        self._device = config.whisper_device
        self._compute_type = config.whisper_compute_type
        self._beam_size = config.whisper_beam_size
        self._model: WhisperModel | None = None

    def _load_model(self) -> WhisperModel:
        if self._model is None:
            logger.info("Loading faster-whisper model: %s (device=%s, compute_type=%s)",
                        self._model_name, self._device, self._compute_type)
            self._model = WhisperModel(self._model_name, device=self._device, compute_type=self._compute_type)
        return self._model

    @staticmethod
    def _extract_audio(video_path: str) -> str:
        """Extract 16 kHz mono WAV from video for faster transcription."""
        audio_path = video_path + ".wav"
        subprocess.run(
            [
                "ffmpeg", "-y",
                "-i", video_path,
                "-vn",                   # drop video
                "-ac", "1",              # mono
                "-ar", "16000",          # 16 kHz (Whisper native rate)
                "-c:a", "pcm_s16le",     # 16-bit PCM
                audio_path,
            ],
            capture_output=True, check=True,
        )
        logger.info("Audio extracted: %s", audio_path)
        return audio_path

    def transcribe(self, video_path: str) -> list[dict]:
        """Transcribe a video file and return timestamped segments.

        Each segment: {"start": float, "end": float, "text": str}
        """
        logger.info("Transcribing: %s", video_path)

        # Pre-extract audio to 16 kHz mono WAV for faster decoding
        audio_path = self._extract_audio(video_path)

        try:
            model = self._load_model()
            segments_iter, info = model.transcribe(
                audio_path,
                language="uk",
                beam_size=self._beam_size,
                vad_filter=True,
                vad_parameters={"min_silence_duration_ms": 500},
                condition_on_previous_text=False,
            )
            segments = [{"start": s.start, "end": s.end, "text": s.text} for s in segments_iter]
            logger.info("Transcription complete: %d segments (detected language: %s, prob=%.2f)",
                         len(segments), info.language, info.language_probability)
            return segments
        finally:
            if os.path.exists(audio_path):
                os.remove(audio_path)
