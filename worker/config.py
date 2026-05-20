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

    # Reframer — dynamic saliency-based 9:16 crop
    reframer_sample_fps: float = float(os.getenv("REFRAMER_SAMPLE_FPS", "5"))
    reframer_smoothing_alpha: float = float(os.getenv("REFRAMER_SMOOTHING_ALPHA", "0.15"))
    reframer_dead_zone_pct: float = float(os.getenv("REFRAMER_DEAD_ZONE_PCT", "0.02"))
    reframer_enable_face_detection: bool = (
        os.getenv("REFRAMER_ENABLE_FACE_DETECTION", "true").lower() == "true"
    )
    # Encoder: "libx264" (default, broadest compatibility) or "h264_nvenc" (GPU, ~3-5x faster).
    # h264_nvenc is probed at startup; falls back to libx264 if not compiled in.
    reframer_encoder: str = os.getenv("REFRAMER_ENCODER", "libx264")
    # Trajectory cache: when true, reuses computed trajectories across reprocessing runs.
    # Cache key = SHA-256(clip_hash + analysis settings). Stored in cache_bucket under trajectories/.
    reframer_cache_enabled: bool = (
        os.getenv("REFRAMER_CACHE_ENABLED", "false").lower() == "true"
    )
    # Conference-aware reframing — layout detection and signal thresholds
    # Number of frames sampled from the start of the clip to locate the split line.
    reframer_layout_sample_frames: int = int(os.getenv("REFRAMER_LAYOUT_SAMPLE_FRAMES", "30"))
    # Canny edge density (0–1) above which the screenshare is considered active.
    reframer_screen_edge_threshold: float = float(os.getenv("REFRAMER_SCREEN_EDGE_THRESHOLD", "0.08"))
    # Mean frame-diff (0–1) above which screenshare motion is detected.
    reframer_screen_motion_threshold: float = float(os.getenv("REFRAMER_SCREEN_MOTION_THRESHOLD", "0.03"))
    # Seconds of continuous screenshare activity required before switching TO screen (fast).
    reframer_switch_to_screen_dwell: float = float(os.getenv("REFRAMER_SWITCH_TO_SCREEN_DWELL", "0.5"))
    # Seconds of screenshare inactivity + speaker presence required before switching AWAY (slow).
    reframer_switch_from_screen_dwell: float = float(os.getenv("REFRAMER_SWITCH_FROM_SCREEN_DWELL", "2.0"))
