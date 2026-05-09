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
        reframer_scene_cut_threshold=0.4,
        reframer_face_boost_sigma_pct=0.10,
        reframer_enable_face_detection=False,
        reframer_cache_enabled=False,
        reframer_encoder="libx264",
        cache_bucket="cache",
    )
    defaults.update(overrides)
    return SimpleNamespace(**defaults)


def _make_reframer(**config_overrides):
    """Instantiate Reframer with cv2 availability patched in."""
    with patch("reframer._cv2_available", True):
        from reframer import Reframer
        return Reframer(_make_config(**config_overrides))


# ===========================================================================
# _best_window_x
# ===========================================================================

class TestBestWindowX:
    def setup_method(self):
        with patch("reframer._cv2_available", True):
            from reframer import Reframer
            self.r = Reframer(_make_config())

    def test_peak_at_left(self):
        energy = np.array([10, 9, 1, 1, 1, 1, 1, 1], dtype=np.float32)
        x = self.r._best_window_x(energy, crop_w=3, x_max=5, fallback=0)
        assert x == 0

    def test_peak_at_right(self):
        energy = np.array([1, 1, 1, 1, 1, 9, 10, 8], dtype=np.float32)
        x = self.r._best_window_x(energy, crop_w=3, x_max=5, fallback=0)
        assert x == 5  # clamped to x_max

    def test_peak_in_middle(self):
        energy = np.array([1, 1, 1, 10, 10, 10, 1, 1], dtype=np.float32)
        x = self.r._best_window_x(energy, crop_w=3, x_max=5, fallback=0)
        assert x == 3

    def test_fallback_when_energy_shorter_than_crop(self):
        energy = np.array([1, 2], dtype=np.float32)
        x = self.r._best_window_x(energy, crop_w=3, x_max=0, fallback=42)
        assert x == 42

    def test_clamped_to_x_max(self):
        energy = np.zeros(10, dtype=np.float32)
        energy[9] = 100.0
        x = self.r._best_window_x(energy, crop_w=3, x_max=4, fallback=0)
        assert x <= 4


# ===========================================================================
# _add_gaussian_boost
# ===========================================================================

class TestAddGaussianBoost:
    def setup_method(self):
        with patch("reframer._cv2_available", True):
            from reframer import Reframer
            self.r = Reframer(_make_config())

    def test_peak_at_face_center(self):
        sal = np.zeros((50, 80), dtype=np.float32)
        boosted = self.r._add_gaussian_boost(sal, cx=40, cy=25, sigma=5)
        peak_y, peak_x = np.unravel_index(np.argmax(boosted), boosted.shape)
        assert peak_x == 40
        assert peak_y == 25

    def test_boost_is_additive(self):
        sal = np.ones((20, 30), dtype=np.float32)
        boosted = self.r._add_gaussian_boost(sal, cx=15, cy=10, sigma=3)
        assert boosted.min() >= 1.0  # baseline preserved
        assert boosted.max() > 1.0   # boost added


# ===========================================================================
# _smooth — EMA, dead-zone, scene cuts, clamping
# ===========================================================================

class TestSmooth:
    def setup_method(self):
        with patch("reframer._cv2_available", True):
            from reframer import Reframer
            self.r = Reframer(_make_config())

    def test_static_input_is_stable(self):
        """All samples at the same x → output must equal that x."""
        raw = [(float(i) * 0.2, 100) for i in range(20)]
        result = self.r._smooth(raw, x_max=500, scene_cuts=set())
        xs = [x for _, x in result]
        assert all(x == 100 for x in xs)

    def test_ema_converges_toward_target(self):
        """Starting at 0, stepping toward 400 should increase monotonically."""
        raw = [(0.0, 0)] + [(float(i + 1) * 0.2, 400) for i in range(30)]
        result = self.r._smooth(raw, x_max=500, scene_cuts=set())
        xs = [x for _, x in result]
        # Values after the first step should be non-decreasing
        assert xs[-1] > xs[0]

    def test_scene_cut_hard_snaps(self):
        """At a scene cut, x must immediately adopt the new raw value."""
        raw = [(0.0, 0), (0.2, 0), (0.4, 300), (0.6, 300)]
        result = self.r._smooth(raw, x_max=500, scene_cuts={2})  # cut at index 2
        assert result[2][1] == 300  # hard snap
        assert result[3][1] == 300

    def test_dead_zone_suppresses_micro_jitter(self):
        """Moves smaller than dead_zone_pct × x_max should not shift the output."""
        # dead_zone = 0.02 × 400 = 8 px
        raw = [(0.0, 100), (0.2, 105), (0.4, 103), (0.6, 100)]  # all Δ ≤ 5 px
        result = self.r._smooth(raw, x_max=400, scene_cuts=set())
        # First value sets prev_x = 100; subsequent deltas are ≤ 5 < 8 → no move
        assert all(x == 100 for _, x in result)

    def test_clamped_to_zero_and_x_max(self):
        """Output x must never go below 0 or above x_max."""
        raw = [(0.0, -50), (0.2, 9999)]
        result = self.r._smooth(raw, x_max=200, scene_cuts={0, 1})
        xs = [x for _, x in result]
        assert all(0 <= x <= 200 for x in xs)

    def test_output_length_matches_input(self):
        raw = [(float(i) * 0.2, 50) for i in range(10)]
        result = self.r._smooth(raw, x_max=400, scene_cuts=set())
        assert len(result) == len(raw)


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
