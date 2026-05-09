import os


class Config:
    """Centralized configuration loaded from environment variables."""

    # Kafka
    kafka_bootstrap_servers: str = os.getenv("KAFKA_BOOTSTRAP_SERVERS", "kafka:29092")
    kafka_topic: str = os.getenv("KAFKA_TOPIC", "video-processing")
    kafka_completion_topic: str = os.getenv("KAFKA_COMPLETION_TOPIC", "video-processing-completed")
    kafka_group_id: str = os.getenv("KAFKA_GROUP_ID", "worker-group")
    kafka_max_poll_interval_ms: int = int(os.getenv("KAFKA_MAX_POLL_INTERVAL_MS", "1800000"))
    kafka_session_timeout_ms: int = int(os.getenv("KAFKA_SESSION_TIMEOUT_MS", "60000"))

    # MinIO
    minio_endpoint: str = os.getenv("MINIO_ENDPOINT", "minio:9000")
    minio_access_key: str = os.getenv("MINIO_ACCESS_KEY", "admin")
    minio_secret_key: str = os.getenv("MINIO_SECRET_KEY", "password123")
    minio_secure: bool = os.getenv("MINIO_SECURE", "false").lower() == "true"

    # Ollama
    ollama_url: str = os.getenv("OLLAMA_URL", "http://ollama:11434/api/generate")
    ollama_model: str = os.getenv("OLLAMA_MODEL", "gemma4:e4b")

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
    reframer_scene_cut_threshold: float = float(os.getenv("REFRAMER_SCENE_CUT_THRESHOLD", "0.4"))
    reframer_face_boost_sigma_pct: float = float(os.getenv("REFRAMER_FACE_BOOST_SIGMA_PCT", "0.10"))
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
