---
title: ShoMark Knowledge Base
tags:
  - index
  - shomark
created: 2026-04-25
---

# ShoMark Knowledge Base

Developer documentation for the ShoMark video highlight extraction and campaign management system.

## Infrastructure

| Note | Description |
|------|-------------|
| [[microservices-architecture]] | Gateway BFF, service boundaries, databases, and Kafka event contracts |
| [[keycloak]] | Set up the Keycloak realm, client, and test users for JWT authentication |
| [[ollama]] | Run the local Ollama LLM server and manage models for highlight detection |

## Worker Performance

| Feature | Details |
|---------|----------|
| **Transcript cache** | After Whisper transcription, segments are cached in MinIO bucket `cache` under `transcripts/{sha256}.json`. Cache key = SHA-256(file content + Whisper settings). Subsequent reprocessing of the same video skips Whisper entirely. Controlled by `CACHE_BUCKET` and `CACHE_ENABLED` env vars. |
| **Dynamic 9:16 reframing** | `worker/reframer.py` replaces the static center-crop with a two-pass saliency-based pipeline. Pass 1: lossless `-c copy` cut. Pass 2: sample frames at `REFRAMER_SAMPLE_FPS` (default 5 fps), compute `StaticSaliencyFineGrained` + optional MediaPipe face-boost, slide a 9:16 window over the 1-D energy profile to find optimal crop x, smooth with EMA (α=`REFRAMER_SMOOTHING_ALPHA`, default 0.15) and dead-zone (`REFRAMER_DEAD_ZONE_PCT`), hard-snap at scene cuts detected via HSV histogram χ² (`REFRAMER_SCENE_CUT_THRESHOLD`), then re-encode with FFmpeg `sendcmd` driving the `crop x` parameter dynamically. |
| **Reframer encoder** | Configured via `REFRAMER_ENCODER` (default `libx264`). Set to `h264_nvenc` for GPU-accelerated encoding (~3-5× faster). The encoder is probed at startup; falls back to `libx264` with a warning if NVENC is not compiled into the available FFmpeg binary. CPU-only container image: `docker build --build-arg BASE_IMAGE=python:3.10-slim-bookworm`. |
| **Trajectory cache** | Opt-in via `REFRAMER_CACHE_ENABLED=true` (default off). Stores computed reframing trajectories to MinIO `cache_bucket` under `trajectories/{sha256}.json`. Cache key = SHA-256(clip content hash + all analysis settings). Encoder is excluded from the key (trajectory is encoder-agnostic). Mirrors the transcript-cache pattern in `StorageClient`. |
| **Batched Whisper** | faster-whisper `BatchedInferencePipeline` is used when `WHISPER_BATCH_SIZE > 0` (default 16). Requires faster-whisper ≥ 1.0.0. Significantly faster on GPU for long videos. |
| **LLM optimizations** | HTTP session reuse, `OLLAMA_TIMEOUT`, `OLLAMA_NUM_PREDICT` cap, `format:"json"` on every request, and transcript block compaction (max 60 blocks × 300 chars each per map chunk). |
| **Benchmark script** | `worker/benchmark.py` — run against a local video file to measure cold vs warm pipeline timing. See worker README for usage. |

## Client Preview

| Feature | Details |
|---------|----------|
| **Generated clip preview** | React clip thumbnails in campaign creation open a modal video preview on click. The client lazily calls `GET /api/fragments/{id}/clip-url`, which returns a presigned MinIO URL for the fragment `StorageKey`. Thumbnail images continue to use `GET /api/fragments/{id}/thumbnail-url`. |
| **Campaign analytics dashboard** | `CampaignDetailsPage` now renders KPI cards, a per-platform bar chart, a trend line chart, and a per-post DataTable. Data comes from `GET /api/analytics/campaigns/{id}` served by the new Analytics microservice. |

## Integrations

| Note | Description |
|------|-------------|
| [[social-media-integration]] | Architecture overview of the OAuth + Kafka publishing pipeline |
| [[oauth-credentials]] | Step-by-step guide to obtaining platform OAuth credentials |

## Quick Links

- [[keycloak#5. Get a Token (Testing)|Get a Keycloak test token]]
- [[ollama#2. Install a Model|Install an Ollama model]]
- [[social-media-integration#OAuth Flow (Step by Step)|OAuth flow step by step]]
- [[oauth-credentials#Production Checklist|Production checklist]]
