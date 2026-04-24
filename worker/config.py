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
    ollama_model: str = os.getenv("OLLAMA_MODEL", "qwen3:30b-a3b")

    # Processing
    clip_duration: int = int(os.getenv("CLIP_DURATION", "60"))
    whisper_model: str = os.getenv("WHISPER_MODEL", "base")
    whisper_device: str = os.getenv("WHISPER_DEVICE", "cpu")
    whisper_compute_type: str = os.getenv("WHISPER_COMPUTE_TYPE", "int8")
    whisper_beam_size: int = int(os.getenv("WHISPER_BEAM_SIZE", "1"))
    map_chunks: int = int(os.getenv("MAP_CHUNKS", "3"))
    top_highlights: int = int(os.getenv("TOP_HIGHLIGHTS", "3"))
    worker_concurrency: int = int(os.getenv("WORKER_CONCURRENCY", "2"))
