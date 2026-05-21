"""Unit tests for Reframer — pure helpers only (no cv2 / FFmpeg / network I/O)."""
import hashlib
from types import SimpleNamespace
from unittest.mock import MagicMock, patch

import numpy as np
import pytest

# ---------------------------------------------------------------------------
# Minimal config fixture
# ---------------------------------------------------------------------------

def _make_config(**overrides):
    defaults = dict(
        reframer_sample_fps=5.0,
        reframer_smoothing_alpha=0.15,
        reframer_dead_zone_pct=0.02,
        reframer_enable_face_detection=False,
        reframer_cache_enabled=False,
        reframer_encoder="libx264",
        cache_bucket="cache",
        reframer_layout_sample_frames=30,
        reframer_screen_edge_threshold=0.08,
        reframer_screen_motion_threshold=0.03,
        reframer_switch_to_screen_dwell=0.5,
        reframer_switch_from_screen_dwell=2.0,
    )
    defaults.update(overrides)
    return SimpleNamespace(**defaults)


def _make_reframer(**config_overrides):
    """Instantiate Reframer with cv2 availability patched in."""
    with patch("reframer._cv2_available", True):
        from reframer import Reframer
        return Reframer(_make_config(**config_overrides))


# ===========================================================================
# _region_crop_x
# ===========================================================================

class TestRegionCropX:
    def setup_method(self):
        with patch("reframer._cv2_available", True):
            from reframer import Reframer
            self.r = Reframer(_make_config())

    def test_centres_window_on_region(self):
        # region 0-1920 (left side of a 1920x1080 frame), crop_w=607 (9:16 of 1080)
        x = self.r._region_crop_x((0, 960), crop_w=607, x_max=1313)
        # region centre = 480, x = 480 - 303 = 177
        assert x == 177

    def test_clamps_to_zero(self):
        # region starting at 0 with a very wide crop_w
        x = self.r._region_crop_x((0, 100), crop_w=500, x_max=500)
        assert x == 0

    def test_clamps_to_x_max(self):
        # region near the right edge
        x = self.r._region_crop_x((1800, 1920), crop_w=607, x_max=1313)
        assert x == 1313

    def test_narrow_region_center(self):
        x = self.r._region_crop_x((400, 600), crop_w=100, x_max=1000)
        # centre = 500, x = 500 - 50 = 450
        assert x == 450


# ===========================================================================
# _resolve_encoder — fallback behaviour
# ===========================================================================

class TestResolveEncoder:
    def test_libx264_returned_directly(self):
        with patch("reframer._cv2_available", True):
            from reframer import Reframer
            assert Reframer._resolve_encoder("libx264") == "libx264"

    def test_nvenc_returned_when_available(self):
        mock_result = MagicMock()
        mock_result.stdout = "... h264_nvenc (codec h264) ..."
        with patch("subprocess.run", return_value=mock_result):
            with patch("reframer._cv2_available", True):
                from reframer import Reframer
                assert Reframer._resolve_encoder("h264_nvenc") == "h264_nvenc"

    def test_nvenc_falls_back_to_libx264_when_missing(self):
        mock_result = MagicMock()
        mock_result.stdout = "... libx264 ..."
        with patch("subprocess.run", return_value=mock_result):
            with patch("reframer._cv2_available", True):
                from reframer import Reframer
                assert Reframer._resolve_encoder("h264_nvenc") == "libx264"

    def test_nvenc_falls_back_when_ffmpeg_not_found(self):
        with patch("subprocess.run", side_effect=FileNotFoundError):
            with patch("reframer._cv2_available", True):
                from reframer import Reframer
                assert Reframer._resolve_encoder("h264_nvenc") == "libx264"


# ===========================================================================
# Trajectory cache — hit / miss behaviour
# ===========================================================================

class TestTrajectoryCache:
    def _make_cached_reframer(self, storage):
        with patch("reframer._cv2_available", True):
            from reframer import Reframer
            r = Reframer(_make_config(reframer_cache_enabled=True), storage=storage)
        return r

    def test_cache_hit_skips_sampling(self):
        """When storage returns a cached trajectory, _sample_trajectory must NOT be called."""
        cached = [[0.0, 100], [0.5, 120], [1.0, 110]]
        storage = MagicMock()
        storage.load_trajectory_cache.return_value = cached

        r = self._make_cached_reframer(storage)
        with patch.object(r, "_sample_trajectory") as mock_sample, \
             patch("reframer.StorageClient") as mock_sc:
            mock_sc.compute_content_hash.return_value = "abc123"
            result = r.compute_trajectory("/tmp/fake.mp4")

        mock_sample.assert_not_called()
        assert result == [(0.0, 100), (0.5, 120), (1.0, 110)]

    def test_cache_miss_computes_and_saves(self):
        """On a cache miss the trajectory is computed and then saved to storage."""
        storage = MagicMock()
        storage.load_trajectory_cache.return_value = None
        expected = [(0.0, 50), (0.2, 60)]

        r = self._make_cached_reframer(storage)
        with patch.object(r, "_sample_trajectory", return_value=expected), \
             patch("reframer.StorageClient") as mock_sc:
            mock_sc.compute_content_hash.return_value = "def456"
            result = r.compute_trajectory("/tmp/fake.mp4")

        assert result == expected
        storage.save_trajectory_cache.assert_called_once()

    def test_cache_disabled_never_touches_storage(self):
        """When cache is disabled, storage must not be consulted."""
        storage = MagicMock()
        expected = [(0.0, 80)]

        with patch("reframer._cv2_available", True):
            from reframer import Reframer
            r = Reframer(_make_config(reframer_cache_enabled=False), storage=storage)

        with patch.object(r, "_sample_trajectory", return_value=expected):
            result = r.compute_trajectory("/tmp/fake.mp4")

        storage.load_trajectory_cache.assert_not_called()
        storage.save_trajectory_cache.assert_not_called()
        assert result == expected


# ===========================================================================
# _build_cache_key — determinism
# ===========================================================================

class TestBuildCacheKey:
    def test_same_inputs_produce_same_key(self):
        r = _make_reframer()
        k1 = r._build_cache_key("hash1")
        k2 = r._build_cache_key("hash1")
        assert k1 == k2

    def test_different_content_hash_produces_different_key(self):
        r = _make_reframer()
        assert r._build_cache_key("hash1") != r._build_cache_key("hash2")

    def test_different_settings_produce_different_key(self):
        r1 = _make_reframer(reframer_sample_fps=5.0)
        r2 = _make_reframer(reframer_sample_fps=10.0)
        assert r1._build_cache_key("hash1") != r2._build_cache_key("hash1")

    def test_different_dwell_produces_different_key(self):
        r1 = _make_reframer(reframer_switch_to_screen_dwell=0.5)
        r2 = _make_reframer(reframer_switch_to_screen_dwell=1.5)
        assert r1._build_cache_key("hash1") != r2._build_cache_key("hash1")

    def test_key_starts_with_trajectories_prefix(self):
        r = _make_reframer()
        assert r._build_cache_key("hash1").startswith("trajectories/")


# ===========================================================================
# _interpolate_trajectory — smooth per-frame densification
# ===========================================================================

class TestInterpolateTrajectory:
    def setup_method(self):
        with patch("reframer._cv2_available", True):
            from reframer import Reframer
            self.r = Reframer(_make_config())

    def test_single_point_returned_unchanged(self):
        traj = [(0.0, 100)]
        result = self.r._interpolate_trajectory(traj, output_fps=30.0)
        assert result == [(0.0, 100)]

    def test_output_length_matches_frame_count(self):
        # 1-second clip at 30 fps → 31 frames (0..30 inclusive)
        traj = [(0.0, 0), (1.0, 300)]
        result = self.r._interpolate_trajectory(traj, output_fps=30.0)
        assert len(result) == 31

    def test_interpolation_is_monotone_between_endpoints(self):
        traj = [(0.0, 0), (1.0, 300)]
        result = self.r._interpolate_trajectory(traj, output_fps=30.0)
        xs = [x for _, x in result]
        assert xs[0] == 0
        assert xs[-1] == 300
        # monotonically non-decreasing
        assert all(xs[i] <= xs[i + 1] for i in range(len(xs) - 1))

    def test_static_trajectory_stays_constant(self):
        traj = [(0.0, 150), (0.5, 150), (1.0, 150)]
        result = self.r._interpolate_trajectory(traj, output_fps=30.0)
        assert all(x == 150 for _, x in result)

    def test_timestamps_span_original_range(self):
        traj = [(2.0, 50), (3.0, 200)]
        result = self.r._interpolate_trajectory(traj, output_fps=10.0)
        assert abs(result[0][0] - 2.0) < 1e-6
        assert abs(result[-1][0] - 3.0) < 1e-6


# ===========================================================================
# _detect_layout_type — layout classification heuristic
# ===========================================================================

class TestDetectLayoutType:
    """Tests use synthetic numpy frames — no video I/O or cv2.VideoCapture."""

    def _make_frames_with_edges(self, height: int, width: int, edge_side: str, n: int = 5):
        """Create N identical frames with dense edges on one side and a blank other side.

        *edge_side* is ``'left'`` or ``'right'``.  The edge side contains a
        white-on-black text-like pattern (alternating rows/cols); the other
        side is solid grey (uniform, no edges).
        """
        frames = []
        for _ in range(n):
            frame = np.full((height, width, 3), 128, dtype=np.uint8)
            mid = width // 2
            if edge_side == "left":
                # Checkerboard on left half — dense Canny edges
                frame[:, :mid:2, :] = 255
                frame[::2, :mid, :] = 0
            else:
                # Checkerboard on right half
                frame[:, mid::2, :] = 255
                frame[::2, mid:, :] = 0
        frames.append(frame)
        return frames

    def _call(self, frames, frame_w: int) -> str:
        with patch("reframer._cv2_available", True):
            from reframer import Reframer
        return Reframer._detect_layout_type(frames, frame_w)

    def test_dominant_left_half_returns_screenshare_pip(self):
        frames = self._make_frames_with_edges(360, 640, "left")
        result = self._call(frames, 640)
        assert result == "screenshare_pip"

    def test_dominant_right_half_returns_screenshare_pip(self):
        frames = self._make_frames_with_edges(360, 640, "right")
        result = self._call(frames, 640)
        assert result == "screenshare_pip"

    def test_balanced_edges_returns_side_by_side(self):
        # Both halves have identical checkerboard — ratio ≈ 1.0
        height, width = 360, 640
        frame = np.zeros((height, width, 3), dtype=np.uint8)
        frame[::2, ::2, :] = 255  # uniform checkerboard across full width
        result = self._call([frame] * 5, width)
        assert result == "side_by_side"

    def test_blank_frame_returns_side_by_side(self):
        # All-grey frames → no edges anywhere → ratio stays at ~1 → side_by_side
        frame = np.full((360, 640, 3), 128, dtype=np.uint8)
        result = self._call([frame] * 5, 640)
        assert result == "side_by_side"