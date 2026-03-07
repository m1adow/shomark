import os
import logging
from minio import Minio

from config import Config

logger = logging.getLogger(__name__)


class StorageClient:
    """MinIO storage: download source videos, upload highlight clips."""

    def __init__(self, config: Config) -> None:
        self._client = Minio(
            config.minio_endpoint,
            access_key=config.minio_access_key,
            secret_key=config.minio_secret_key,
            secure=config.minio_secure,
        )

    def download_video(self, bucket: str, key: str, local_path: str) -> str:
        """Download a video from MinIO to a local file. Returns the local path."""
        logger.info("Downloading %s/%s -> %s", bucket, key, local_path)
        os.makedirs(os.path.dirname(local_path) or ".", exist_ok=True)
        self._client.fget_object(bucket, key, local_path)
        logger.info("Download complete: %s", local_path)
        return local_path

    def upload_file(self, bucket: str, key: str, local_path: str, content_type: str = "video/mp4") -> None:
        """Upload a local file to MinIO, creating the bucket if needed."""
        if not self._client.bucket_exists(bucket):
            self._client.make_bucket(bucket)
            logger.info("Created bucket: %s", bucket)

        logger.info("Uploading %s -> %s/%s", local_path, bucket, key)
        self._client.fput_object(bucket, key, local_path, content_type=content_type)
        logger.info("Upload complete: %s/%s", bucket, key)
