import os


def _require(name: str) -> str:
    value = os.getenv(name)
    if not value:
        raise RuntimeError(f"Required environment variable '{name}' is not set.")
    return value


class Config:
    """Centralized configuration loaded from environment variables."""

    # Kafka — broker address is required; topics and timeouts have sensible defaults
    kafka_bootstrap_servers: str = _require("KAFKA_BOOTSTRAP_SERVERS")
    kafka_topic: str = os.getenv("KAFKA_TOPIC", "video-processing")
    kafka_completion_topic: str = os.getenv("KAFKA_COMPLETION_TOPIC", "video-processing-completed")
    kafka_transcription_topic: str = os.getenv("KAFKA_TRANSCRIPTION_TOPIC", "video-transcription")
    kafka_summarization_topic: str = os.getenv("KAFKA_SUMMARIZATION_TOPIC", "video-summarization-completed")
    kafka_group_id: str = os.getenv("KAFKA_GROUP_ID", "worker-group")
    kafka_max_poll_interval_ms: int = int(os.getenv("KAFKA_MAX_POLL_INTERVAL_MS", "1800000"))
    kafka_session_timeout_ms: int = int(os.getenv("KAFKA_SESSION_TIMEOUT_MS", "60000"))

    # MinIO — endpoint and credentials are required
    minio_endpoint: str = _require("MINIO_ENDPOINT")
    minio_access_key: str = _require("MINIO_ACCESS_KEY")
    minio_secret_key: str = _require("MINIO_SECRET_KEY")
    minio_secure: bool = os.getenv("MINIO_SECURE", "false").lower() == "true"

    # Ollama — URL and primary model are required
    ollama_url: str = _require("OLLAMA_URL")
    ollama_model: str = _require("OLLAMA_MODEL")
    # Separate (typically smaller/faster) model used only for the Phase-1 summary.
    # Defaults to the primary model if not set.
    ollama_summary_model: str = os.getenv("OLLAMA_SUMMARY_MODEL") or os.getenv("OLLAMA_MODEL", "")

    # Processing
    clip_duration: int = int(os.getenv("CLIP_DURATION", "60"))
    whisper_model: str = os.getenv("WHISPER_MODEL", "base")
    whisper_device: str = os.getenv("WHISPER_DEVICE", "cpu")
    whisper_compute_type: str = os.getenv("WHISPER_COMPUTE_TYPE", "int8")
    whisper_beam_size: int = int(os.getenv("WHISPER_BEAM_SIZE", "1"))
    map_chunks: int = int(os.getenv("MAP_CHUNKS", "3"))
    top_highlights: int = int(os.getenv("TOP_HIGHLIGHTS", "3"))
    worker_concurrency: int = int(os.getenv("WORKER_CONCURRENCY", "2"))

    # Transcript cache (MinIO)
    cache_bucket: str = os.getenv("CACHE_BUCKET", "cache")
    cache_enabled: bool = os.getenv("CACHE_ENABLED", "true").lower() == "true"

    # Ollama performance
    ollama_timeout: int = int(os.getenv("OLLAMA_TIMEOUT", "300"))
    ollama_num_predict: int = int(os.getenv("OLLAMA_NUM_PREDICT", "1024"))

    # Whisper batched inference (0 = disabled, use standard transcribe)
    whisper_batch_size: int = int(os.getenv("WHISPER_BATCH_SIZE", "16"))

    # Encoder for 9:16 clip output: "libx264" (default) or "h264_nvenc" (GPU, ~3-5x faster)
    reframer_encoder: str = os.getenv("REFRAMER_ENCODER", "libx264")
