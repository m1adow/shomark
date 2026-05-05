import hashlib
import io
import json
import os
import logging
from minio import Minio
from minio.error import S3Error

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

    # ------------------------------------------------------------------
    # Transcript cache helpers
    # ------------------------------------------------------------------

    @staticmethod
    def compute_content_hash(local_path: str) -> str:
        """Return the SHA-256 hex digest of a local file's contents.

        Reading is done in 1 MB chunks to avoid loading large videos into RAM.
        """
        h = hashlib.sha256()
        with open(local_path, "rb") as fh:
            for chunk in iter(lambda: fh.read(1 << 20), b""):
                h.update(chunk)
        return h.hexdigest()

    def load_transcript_cache(self, bucket: str, key: str) -> list[dict] | None:
        """Load cached transcript segments from MinIO.

        Returns the deserialized segment list on a hit, or None on a miss /
        any error so callers can always fall back to Whisper.
        """
        try:
            if not self._client.bucket_exists(bucket):
                return None
            response = self._client.get_object(bucket, key)
            try:
                data = json.loads(response.read().decode("utf-8"))
            finally:
                response.close()
                response.release_conn()
            logger.info("Transcript cache HIT: %s/%s (%d segments)", bucket, key, len(data))
            return data
        except S3Error:
            return None
        except Exception as exc:
            logger.warning("Transcript cache load failed (%s/%s): %s", bucket, key, exc)
            return None

    def save_transcript_cache(self, bucket: str, key: str, segments: list[dict]) -> None:
        """Persist transcript segments to MinIO as a JSON object."""
        if not self._client.bucket_exists(bucket):
            self._client.make_bucket(bucket)
            logger.info("Created cache bucket: %s", bucket)
        payload = json.dumps(segments, ensure_ascii=False).encode("utf-8")
        self._client.put_object(
            bucket, key, io.BytesIO(payload), length=len(payload),
            content_type="application/json",
        )
        logger.info("Transcript cache written: %s/%s (%d bytes)", bucket, key, len(payload))
