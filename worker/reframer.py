import hashlib
import logging
import os
import subprocess

import numpy as np

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Optional heavy dependencies — graceful fallbacks
# ---------------------------------------------------------------------------
try:
    import cv2  # type: ignore

    _cv2_available = True
except ImportError:
    cv2 = None  # type: ignore
    _cv2_available = False

_mediapipe_available = False
try:
    import mediapipe as _mp
    # Verify the solutions.face_detection API is present.
    # It was removed in MediaPipe >= 0.10.x; accessing it here raises AttributeError
    # on those versions so we disable the boost rather than crashing at runtime.
    _ = _mp.solutions.face_detection
    _mediapipe_available = True
except ImportError:
    logger.info("MediaPipe not installed — face-detection boost disabled")
except AttributeError:
    logger.warning(
        "MediaPipe is installed but mp.solutions.face_detection is not available "
        "(MediaPipe >= 0.10 removed the solutions API). "
        "Install mediapipe<0.11 or disable face detection. Falling back to saliency-only."
    )


class Reframer:
    """Saliency-based dynamic 9:16 reframing for highlight clips.

    Replaces the static center-crop in VideoProcessor with a two-step pipeline:

    1. **Trajectory computation** — sample frames at *sample_fps*; for each
       frame build a per-row saliency map (OpenCV ``StaticSaliencyFineGrained``)
       optionally boosted by a Gaussian at detected face centres (MediaPipe).
       Project the 2-D map to a 1-D horizontal energy profile and slide a
       9:16-wide window to find the crop x that maximises interest.  Detect
       scene cuts via HSV-histogram chi-square distance and snap hard at those
       boundaries; between cuts, apply EMA smoothing with a dead-zone to
       suppress micro-jitter.

    2. **Render** — write an FFmpeg ``sendcmd`` script that drives the ``crop``
       filter's *x* parameter frame-by-frame, then re-encode using the
       configured encoder (``libx264`` default; ``h264_nvenc`` opt-in with
       automatic fallback to ``libx264`` when NVENC is not compiled in).

    Trajectory cache (opt-in via ``reframer_cache_enabled``) stores the
    computed ``[[t, x], …]`` trajectory to MinIO under ``trajectories/`` inside
    the existing ``cache_bucket``, keyed by SHA-256 of the clip content hash
    combined with all analysis settings (encoder is excluded — the trajectory is
    encoder-agnostic).
    """

    def __init__(self, config, storage=None) -> None:  # noqa: ANN001
        if not _cv2_available:
            raise RuntimeError(
                "opencv-contrib-python is required for Reframer. "
                "Install it via: pip install opencv-contrib-python"
            )

        self._sample_fps: float = config.reframer_sample_fps
        self._alpha: float = config.reframer_smoothing_alpha
        self._dead_zone_pct: float = config.reframer_dead_zone_pct
        self._scene_threshold: float = config.reframer_scene_cut_threshold
        self._face_sigma_pct: float = config.reframer_face_boost_sigma_pct
        self._enable_face: bool = config.reframer_enable_face_detection and _mediapipe_available
        self._cache_enabled: bool = config.reframer_cache_enabled
        self._cache_bucket: str = config.cache_bucket
        self._storage = storage
        self._encoder: str = self._resolve_encoder(config.reframer_encoder)

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def compute_trajectory(self, video_path: str) -> list[tuple[float, int]]:
        """Return ``[(timestamp_seconds, crop_x), …]`` for the clip.

        *crop_x* is the left edge of the 9:16 window
        (``0 ≤ x ≤ frame_w − crop_w``).
        """
        cache_key: str | None = None
        if self._cache_enabled and self._storage is not None:
            from storage import StorageClient  # local import avoids circular ref

            content_hash = StorageClient.compute_content_hash(video_path)
            cache_key = self._build_cache_key(content_hash)
            cached = self._storage.load_trajectory_cache(self._cache_bucket, cache_key)
            if cached is not None:
                logger.info("Trajectory cache HIT: %s", cache_key)
                return [(float(t), int(x)) for t, x in cached]

        trajectory = self._sample_trajectory(video_path)

        if cache_key is not None:
            self._storage.save_trajectory_cache(self._cache_bucket, cache_key, trajectory)

        return trajectory

    def render(
        self,
        input_path: str,
        trajectory: list[tuple[float, int]],
        output_path: str,
        output_fps: float = 30.0,
    ) -> None:
        """Re-encode *input_path* applying the dynamic crop trajectory to *output_path*.

        The sparse trajectory (sampled at *reframer_sample_fps*) is first
        interpolated to *output_fps* resolution so that the FFmpeg ``sendcmd``
        filter updates crop-x on every single output frame, producing
        continuously smooth panning instead of visible stepped jumps.
        """
        if not trajectory:
            trajectory = [(0.0, 0)]

        # Interpolate sparse keyframes → per-output-frame timestamps
        dense = self._interpolate_trajectory(trajectory, output_fps)

        cmd_file = input_path + ".crops.cmd"
        try:
            self._write_sendcmd(dense, cmd_file)
            initial_x = dense[0][1]

            vf = (
                f"sendcmd=f={cmd_file},"
                f"crop=ih*9/16:ih:{initial_x}:0,"
                "scale=1080:1920,"
                "setsar=1"
            )

            ffmpeg_cmd = [
                "ffmpeg", "-y",
                "-i", input_path,
                "-vf", vf,
                "-r", "30",
                *self._encoder_flags(),
                "-c:a", "aac", "-b:a", "128k",
                "-ar", "44100",
                "-movflags", "+faststart",
                "-avoid_negative_ts", "make_zero",
                output_path,
            ]

            logger.info("Rendering reframed clip (encoder=%s): %s", self._encoder, output_path)
            subprocess.run(ffmpeg_cmd, capture_output=True, check=True)
        finally:
            if os.path.exists(cmd_file):
                os.remove(cmd_file)

    # ------------------------------------------------------------------
    # Trajectory sampling
    # ------------------------------------------------------------------

    def _sample_trajectory(self, video_path: str) -> list[tuple[float, int]]:
        cap = cv2.VideoCapture(video_path)
        try:
            src_fps: float = cap.get(cv2.CAP_PROP_FPS) or 25.0
            frame_w: int = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
            frame_h: int = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        except Exception:
            cap.release()
            raise

        crop_w = max(1, round(frame_h * 9 / 16))
        x_max = max(0, frame_w - crop_w)
        x_default = x_max // 2

        step = max(1, round(src_fps / self._sample_fps))
        face_sigma_px = max(1, round(frame_w * self._face_sigma_pct))

        saliency_algo = cv2.saliency.StaticSaliencyFineGrained_create()

        face_detector = None
        if self._enable_face:
            import mediapipe as mp  # noqa: PLC0415

            face_detector = mp.solutions.face_detection.FaceDetection(
                model_selection=0, min_detection_confidence=0.5
            )

        raw_points: list[tuple[float, int]] = []
        scene_cuts: set[int] = set()
        prev_hist: np.ndarray | None = None
        idx = 0
        frame_no = 0

        try:
            while True:
                ret, frame = cap.read()
                if not ret:
                    break
                if frame_no % step == 0:
                    t = frame_no / src_fps

                    # Saliency map
                    ok, sal_map = saliency_algo.computeSaliency(frame)
                    sal: np.ndarray = (
                        sal_map.astype(np.float32)
                        if ok
                        else np.ones((frame_h, frame_w), dtype=np.float32)
                    )

                    # Optional face-detection boost
                    if face_detector is not None:
                        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                        results = face_detector.process(rgb)
                        if results.detections:
                            for det in results.detections:
                                bbox = det.location_data.relative_bounding_box
                                cx = int((bbox.xmin + bbox.width / 2) * frame_w)
                                cy = int((bbox.ymin + bbox.height / 2) * frame_h)
                                sal = self._add_gaussian_boost(sal, cx, cy, face_sigma_px)

                    # Best crop window x
                    energy: np.ndarray = sal.sum(axis=0)
                    x = self._best_window_x(energy, crop_w, x_max, x_default)

                    # Scene cut detection via HSV histogram chi-square
                    hsv = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)
                    hist = cv2.calcHist([hsv], [0, 1], None, [30, 32], [0, 180, 0, 256])
                    cv2.normalize(hist, hist)
                    if prev_hist is not None:
                        dist = cv2.compareHist(hist, prev_hist, cv2.HISTCMP_CHISQR)
                        if dist > self._scene_threshold:
                            scene_cuts.add(idx)
                    prev_hist = hist

                    raw_points.append((t, x))
                    idx += 1
                frame_no += 1
        finally:
            cap.release()
            if face_detector is not None:
                face_detector.close()

        if not raw_points:
            return [(0.0, x_default)]

        return self._smooth(raw_points, x_max, scene_cuts)

    # ------------------------------------------------------------------
    # Pure helpers (no I/O — easily unit-testable)
    # ------------------------------------------------------------------

    @staticmethod
    def _best_window_x(
        energy: np.ndarray,
        crop_w: int,
        x_max: int,
        fallback: int,
    ) -> int:
        """Slide a window of width *crop_w* and return the left-edge x
        that maximises the sum of *energy*, clamped to [0, x_max]."""
        if len(energy) <= crop_w:
            return fallback
        cs = np.concatenate(([0.0], np.cumsum(energy)))
        window_sums = cs[crop_w:] - cs[: len(cs) - crop_w]
        return min(int(np.argmax(window_sums)), x_max)

    @staticmethod
    def _add_gaussian_boost(
        sal: np.ndarray,
        cx: int,
        cy: int,
        sigma: int,
    ) -> np.ndarray:
        """Add a unit Gaussian centred at *(cx, cy)* to the saliency map."""
        h, w = sal.shape[:2]
        xs = np.arange(w, dtype=np.float32)
        ys = np.arange(h, dtype=np.float32)
        xx, yy = np.meshgrid(xs, ys)
        gauss = np.exp(-((xx - cx) ** 2 + (yy - cy) ** 2) / (2.0 * sigma ** 2))
        return sal + gauss

    def _smooth(
        self,
        raw: list[tuple[float, int]],
        x_max: int,
        scene_cuts: set[int],
    ) -> list[tuple[float, int]]:
        """EMA smoothing with dead-zone; hard snap at detected scene cuts."""
        smoothed: list[tuple[float, int]] = []
        prev_x = float(raw[0][1])
        dead_zone = x_max * self._dead_zone_pct

        for i, (t, x) in enumerate(raw):
            if i in scene_cuts:
                prev_x = float(x)  # hard snap
            elif abs(x - prev_x) > dead_zone:
                prev_x = self._alpha * x + (1.0 - self._alpha) * prev_x
            # else: dead-zone — hold current position

            clamped = int(round(max(0.0, min(prev_x, float(x_max)))))
            smoothed.append((t, clamped))

        return smoothed

    # ------------------------------------------------------------------
    # FFmpeg / sendcmd helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _interpolate_trajectory(
        trajectory: list[tuple[float, int]],
        output_fps: float,
    ) -> list[tuple[float, int]]:
        """Linearly interpolate a sparse trajectory to per-output-frame resolution.

        This is what turns stepped ``sendcmd`` jumps into smooth panning:
        instead of the crop-x holding still then snapping every 1/sample_fps
        seconds, it advances by a few pixels on every single output frame.
        """
        if len(trajectory) < 2:
            return trajectory

        ts = np.array([t for t, _ in trajectory], dtype=np.float64)
        xs = np.array([x for _, x in trajectory], dtype=np.float64)
        t_start, t_end = ts[0], ts[-1]
        n_frames = max(2, int(round((t_end - t_start) * output_fps)) + 1)
        t_dense = np.linspace(t_start, t_end, n_frames)
        x_dense = np.interp(t_dense, ts, xs)
        return [(float(t), int(round(x))) for t, x in zip(t_dense, x_dense)]

    @staticmethod
    def _write_sendcmd(trajectory: list[tuple[float, int]], path: str) -> None:
        """Write an FFmpeg sendcmd script that updates crop x at each sample."""
        with open(path, "w", encoding="utf-8") as f:
            for t, x in trajectory:
                f.write(f"{t:.6f} crop x {x};\n")

    def _encoder_flags(self) -> list[str]:
        if self._encoder == "h264_nvenc":
            return [
                "-c:v", "h264_nvenc",
                "-preset", "p5",
                "-tune", "hq",
                "-rc", "vbr",
                "-cq", "23",
                "-b:v", "0",
                "-profile:v", "high",
                "-level:v", "4.0",
                "-pix_fmt", "yuv420p",
            ]
        # libx264 (default)
        return [
            "-c:v", "libx264",
            "-profile:v", "high",
            "-level:v", "4.0",
            "-preset", "fast",
            "-crf", "23",
            "-pix_fmt", "yuv420p",
        ]

    # ------------------------------------------------------------------
    # Cache key
    # ------------------------------------------------------------------

    def _build_cache_key(self, content_hash: str) -> str:
        """Derive MinIO object key for this clip's trajectory cache.

        The fingerprint includes all analysis settings that affect the
        trajectory; the encoder is intentionally excluded because the
        trajectory itself is encoder-agnostic.
        """
        fingerprint = (
            f"{content_hash}:"
            f"{self._sample_fps}:{self._alpha}:{self._dead_zone_pct}:"
            f"{self._scene_threshold}:{self._face_sigma_pct}:{self._enable_face}"
        )
        digest = hashlib.sha256(fingerprint.encode()).hexdigest()
        return f"trajectories/{digest}.json"

    # ------------------------------------------------------------------
    # Encoder probe
    # ------------------------------------------------------------------

    @staticmethod
    def _resolve_encoder(requested: str) -> str:
        """Return the encoder to use.

        Falls back to ``libx264`` with a warning when the requested encoder
        (e.g. ``h264_nvenc``) is not compiled into the available FFmpeg binary.
        """
        if requested == "libx264":
            return "libx264"
        try:
            result = subprocess.run(
                ["ffmpeg", "-hide_banner", "-encoders"],
                capture_output=True,
                text=True,
            )
            if "h264_nvenc" in result.stdout:
                logger.info("h264_nvenc is available — using NVENC encoder")
                return "h264_nvenc"
            logger.warning(
                "Encoder '%s' not compiled into FFmpeg; falling back to libx264", requested
            )
        except FileNotFoundError:
            logger.warning("ffmpeg not found during encoder probe; defaulting to libx264")
        return "libx264"
