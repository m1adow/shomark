import logging
import os
import subprocess
import time

from faster_whisper import WhisperModel

from config import Config

logger = logging.getLogger(__name__)

try:
    from faster_whisper import BatchedInferencePipeline
    _BATCHED_AVAILABLE = True
except ImportError:
    _BATCHED_AVAILABLE = False


class Transcriber:
    """faster-whisper based video/audio transcription (Ukrainian)."""

    def __init__(self, config: Config) -> None:
        self._model_name = config.whisper_model
        self._device = config.whisper_device
        self._compute_type = config.whisper_compute_type
        self._beam_size = config.whisper_beam_size
        self._batch_size = config.whisper_batch_size
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
        t0 = time.perf_counter()
        audio_path = self._extract_audio(video_path)
        logger.info("Audio extraction: %.1fs", time.perf_counter() - t0)

        try:
            model = self._load_model()

            use_batched = self._batch_size > 0 and _BATCHED_AVAILABLE
            mode = f"batched(batch_size={self._batch_size})" if use_batched else "standard"
            logger.info("Whisper mode: %s", mode)

            t0 = time.perf_counter()
            if use_batched:
                segments = self._transcribe_batched(model, audio_path)
            else:
                segments = self._transcribe_standard(model, audio_path)
            logger.info("Whisper transcription: %.1fs (%d segments)", time.perf_counter() - t0, len(segments))

            return segments
        finally:
            if os.path.exists(audio_path):
                os.remove(audio_path)

    def _transcribe_standard(self, model: WhisperModel, audio_path: str) -> list[dict]:
        """Standard faster-whisper transcription (single-segment pipeline)."""
        segments_iter, info = model.transcribe(
            audio_path,
            language="uk",
            beam_size=self._beam_size,
            vad_filter=True,
            vad_parameters={"min_silence_duration_ms": 500},
            condition_on_previous_text=False,
        )
        segments = [{"start": s.start, "end": s.end, "text": s.text} for s in segments_iter]
        logger.info(
            "Standard transcription complete: %d segments (lang=%s, prob=%.2f)",
            len(segments), info.language, info.language_probability,
        )
        return segments

    def _transcribe_batched(self, model: WhisperModel, audio_path: str) -> list[dict]:
        """Batched faster-whisper transcription — significantly faster on GPU."""
        pipeline = BatchedInferencePipeline(model=model)  # type: ignore[name-defined]
        segments_iter, info = pipeline.transcribe(
            audio_path,
            language="uk",
            beam_size=self._beam_size,
            batch_size=self._batch_size,
            vad_filter=True,
            vad_parameters={"min_silence_duration_ms": 500},
        )
        segments = [{"start": s.start, "end": s.end, "text": s.text} for s in segments_iter]
        logger.info(
            "Batched transcription complete: %d segments (lang=%s, prob=%.2f)",
            len(segments), info.language, info.language_probability,
        )
        return segments
