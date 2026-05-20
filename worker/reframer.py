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

_face_detection_available = False
_face_cascade = None
if _cv2_available:
    try:
        _cascade = cv2.CascadeClassifier(
            cv2.data.haarcascades + "haarcascade_frontalface_default.xml"
        )
        if _cascade.empty():
            logger.warning("Haar cascade XML not found — face-detection disabled")
        else:
            _face_cascade = _cascade
            _face_detection_available = True
    except Exception as _exc:
        logger.warning("OpenCV face detection unavailable: %s", _exc)


class Reframer:
    """Conference-aware dynamic 9:16 reframing for highlight clips.

    Designed for side-by-side online conference recordings where one half of
    the frame is a screenshare/presentation and the other half is a speaker
    webcam.  Three-phase pipeline:

    1. **Layout detection** — analyses the first N frames to locate the
       vertical split between the screenshare and speaker regions.  Temporal
       column variance finds the boundary; face detection confirms which side
       holds the speaker.

    2. **Trajectory computation** — samples frames at *sample_fps*.  For each
       frame:

       - Screenshare activity = Canny edge density > threshold OR
         frame-to-frame motion > threshold.
       - Speaker activity = face detected in the speaker region.

       An asymmetric state machine decides the target crop region: switch TO
       screenshare after *switch_to_screen_dwell* seconds of screenshare
       activity; switch AWAY only after *switch_from_screen_dwell* seconds of
       inactivity with an active speaker.  Priority: screenshare > speaker.
       Fallback when neither signal is active: hold last position.  EMA
       smoothing produces fluid panning between the two regions.

    3. **Render** — writes an FFmpeg ``sendcmd`` script that drives the
       ``crop`` filter's *x* parameter per frame, then re-encodes using the
       configured encoder (``libx264`` default; ``h264_nvenc`` opt-in with
       automatic fallback).

    Trajectory cache (opt-in via ``reframer_cache_enabled``) stores the
    computed trajectory to MinIO under ``trajectories/`` inside the existing
    ``cache_bucket``, keyed by SHA-256 of the clip content hash combined with
    all analysis settings.
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
        self._enable_face: bool = (
            config.reframer_enable_face_detection and _face_detection_available
        )
        self._cache_enabled: bool = config.reframer_cache_enabled
        self._cache_bucket: str = config.cache_bucket
        self._storage = storage
        self._encoder: str = self._resolve_encoder(config.reframer_encoder)

        # Conference-mode parameters
        self._layout_sample_frames: int = config.reframer_layout_sample_frames
        self._screen_edge_threshold: float = config.reframer_screen_edge_threshold
        self._screen_motion_threshold: float = config.reframer_screen_motion_threshold
        self._switch_to_screen_dwell: float = config.reframer_switch_to_screen_dwell
        self._switch_from_screen_dwell: float = config.reframer_switch_from_screen_dwell

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
    # Layout detection
    # ------------------------------------------------------------------

    def _detect_layout(
        self, video_path: str
    ) -> tuple[tuple[int, int], tuple[int, int]]:
        """Detect screenshare and speaker regions from the first N frames.

        Returns ``(screen_region, speaker_region)`` where each region is an
        ``(x_start, x_end)`` pixel range.  Strategy:

        1. Compute per-column temporal variance across the sampled frames.
           The static vertical divider between the two panes has the lowest
           variance in the centre of the frame.
        2. Run face detection on the first 10 frames; the side with more face
           detections is the speaker; the other side is the screenshare.
        """
        cap = cv2.VideoCapture(video_path)
        frame_w: int = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        frames: list[np.ndarray] = []
        for _ in range(self._layout_sample_frames):
            ret, frame = cap.read()
            if not ret:
                break
            frames.append(frame)
        cap.release()

        if len(frames) < 2:
            split_x = int(frame_w * 0.7)
            logger.warning(
                "Layout detection: too few frames — assuming 70/30 split at x=%d", split_x
            )
            return (0, split_x), (split_x, frame_w)

        # Temporal column variance → find the low-variance boundary column
        gray_frames = [
            cv2.cvtColor(f, cv2.COLOR_BGR2GRAY).astype(np.float32) for f in frames
        ]
        stack = np.stack(gray_frames, axis=0)  # (N, H, W)
        col_variance = stack.var(axis=0).mean(axis=0)  # (W,)

        # Search only in the central 30–80% of the frame width to avoid edges
        search_start = int(frame_w * 0.30)
        search_end = int(frame_w * 0.80)
        split_x = search_start + int(np.argmin(col_variance[search_start:search_end]))

        logger.info("Layout detection: split_x=%d / frame_w=%d", split_x, frame_w)

        # Face counts per side → determine speaker side
        face_counts = {"left": 0, "right": 0}
        if self._enable_face and _face_cascade is not None:
            for frame in frames[: min(10, len(frames))]:
                gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
                try:
                    faces = _face_cascade.detectMultiScale(
                        gray, scaleFactor=1.1, minNeighbors=5, minSize=(30, 30)
                    )
                except cv2.error:
                    # Known OpenCV bug: vector::_M_range_check on certain frame
                    # dimensions. Skip this frame and continue with the others.
                    continue
                for (x_f, _y_f, w_f, _h_f) in faces:
                    if x_f + w_f // 2 < split_x:
                        face_counts["left"] += 1
                    else:
                        face_counts["right"] += 1

        # Speaker = side with more faces; screen = the other side
        if face_counts["left"] >= face_counts["right"]:
            speaker_region: tuple[int, int] = (0, split_x)
            screen_region: tuple[int, int] = (split_x, frame_w)
        else:
            speaker_region = (split_x, frame_w)
            screen_region = (0, split_x)

        logger.info(
            "Layout: screen=%s  speaker=%s  (face_counts=%s)",
            screen_region, speaker_region, face_counts,
        )
        return screen_region, speaker_region

    # ------------------------------------------------------------------
    # Trajectory sampling
    # ------------------------------------------------------------------

    def _sample_trajectory(self, video_path: str) -> list[tuple[float, int]]:
        screen_region, speaker_region = self._detect_layout(video_path)

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

        screen_center_x = self._region_crop_x(screen_region, crop_w, x_max)
        speaker_center_x = self._region_crop_x(speaker_region, crop_w, x_max)

        step = max(1, round(src_fps / self._sample_fps))

        # Hysteresis: minimum consecutive samples required before a mode switch
        switch_to_screen = max(1, round(self._switch_to_screen_dwell * self._sample_fps))
        switch_from_screen = max(1, round(self._switch_from_screen_dwell * self._sample_fps))

        # State machine
        current_mode = "screen"
        screen_active_count = 0
        screen_inactive_count = 0
        current_x = float(screen_center_x)

        raw_points: list[tuple[float, int]] = []
        prev_screen_gray: np.ndarray | None = None
        frame_no = 0

        try:
            while True:
                ret, frame = cap.read()
                if not ret:
                    break

                if frame_no % step != 0:
                    frame_no += 1
                    continue

                t = frame_no / src_fps

                # --- Screenshare activity: edges OR motion ---
                sx0, sx1 = screen_region
                gray_screen = cv2.cvtColor(frame[:, sx0:sx1], cv2.COLOR_BGR2GRAY)
                edges = cv2.Canny(gray_screen, 50, 150)
                edge_density = float(edges.mean()) / 255.0

                motion = 0.0
                if prev_screen_gray is not None:
                    diff = cv2.absdiff(gray_screen, prev_screen_gray)
                    motion = float(diff.mean()) / 255.0
                prev_screen_gray = gray_screen.copy()

                screen_active = (
                    edge_density > self._screen_edge_threshold
                    or motion > self._screen_motion_threshold
                )

                # --- Speaker activity: face present in speaker region ---
                speaker_active = False
                if self._enable_face and _face_cascade is not None:
                    spx0, spx1 = speaker_region
                    gray_speaker = cv2.cvtColor(
                        frame[:, spx0:spx1], cv2.COLOR_BGR2GRAY
                    )
                    min_side = 30
                    if gray_speaker.shape[0] >= min_side and gray_speaker.shape[1] >= min_side:
                        try:
                            faces = _face_cascade.detectMultiScale(
                                gray_speaker, scaleFactor=1.1, minNeighbors=5, minSize=(min_side, min_side)
                            )
                            speaker_active = len(faces) > 0
                        except cv2.error:
                            pass

                # --- Asymmetric hysteresis counters ---
                if screen_active:
                    screen_active_count += 1
                    screen_inactive_count = 0
                else:
                    screen_inactive_count += 1
                    screen_active_count = 0

                # --- State transitions ---
                if current_mode == "speaker" and screen_active_count >= switch_to_screen:
                    current_mode = "screen"
                    screen_active_count = 0
                    logger.debug(
                        "t=%.1f → screen  (edge=%.3f  motion=%.3f)", t, edge_density, motion
                    )
                elif (
                    current_mode == "screen"
                    and not screen_active
                    and speaker_active
                    and screen_inactive_count >= switch_from_screen
                ):
                    current_mode = "speaker"
                    screen_inactive_count = 0
                    logger.debug("t=%.1f → speaker", t)

                # --- Determine target crop-x ---
                if current_mode == "screen":
                    target_x = float(screen_center_x)
                elif speaker_active:
                    target_x = float(speaker_center_x)
                else:
                    target_x = current_x  # hold last position

                # --- EMA smoothing with dead-zone ---
                dead_zone = x_max * self._dead_zone_pct
                if abs(target_x - current_x) > dead_zone:
                    current_x = self._alpha * target_x + (1.0 - self._alpha) * current_x

                raw_points.append((t, int(round(max(0.0, min(current_x, float(x_max)))))))
                frame_no += 1

        finally:
            cap.release()

        if not raw_points:
            return [(0.0, screen_center_x)]

        return raw_points

    # ------------------------------------------------------------------
    # Pure helpers (no I/O — easily unit-testable)
    # ------------------------------------------------------------------

    @staticmethod
    def _region_crop_x(region: tuple[int, int], crop_w: int, x_max: int) -> int:
        """Return the crop left-edge x that centres the 9:16 window on *region*."""
        r_start, r_end = region
        region_center = (r_start + r_end) // 2
        x = region_center - crop_w // 2
        return int(max(0, min(x, x_max)))

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
        """Derive MinIO object key for this clip's trajectory cache."""
        fingerprint = (
            f"{content_hash}:"
            f"{self._sample_fps}:{self._alpha}:{self._dead_zone_pct}:"
            f"{self._screen_edge_threshold}:{self._screen_motion_threshold}:"
            f"{self._switch_to_screen_dwell}:{self._switch_from_screen_dwell}:"
            f"{self._layout_sample_frames}:{self._enable_face}"
        )
        digest = hashlib.sha256(fingerprint.encode()).hexdigest()
        return f"trajectories/{digest}.json"

    # ------------------------------------------------------------------
    # Encoder probe
    # ------------------------------------------------------------------

    @staticmethod
    def _resolve_encoder(requested: str) -> str:
        """Return the encoder to use, falling back to ``libx264`` if unavailable."""
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