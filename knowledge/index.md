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
| [[keycloak]] | Set up the Keycloak realm, client, and test users for JWT authentication |
| [[ollama]] | Run the local Ollama LLM server and manage models for highlight detection |

## Worker Performance

| Feature | Details |
|---------|----------|
| **Transcript cache** | After Whisper transcription, segments are cached in MinIO bucket `cache` under `transcripts/{sha256}.json`. Cache key = SHA-256(file content + Whisper settings). Subsequent reprocessing of the same video skips Whisper entirely. Controlled by `CACHE_BUCKET` and `CACHE_ENABLED` env vars. |
| **Batched Whisper** | faster-whisper `BatchedInferencePipeline` is used when `WHISPER_BATCH_SIZE > 0` (default 16). Requires faster-whisper ≥ 1.0.0. Significantly faster on GPU for long videos. |
| **LLM optimizations** | HTTP session reuse, `OLLAMA_TIMEOUT`, `OLLAMA_NUM_PREDICT` cap, `format:"json"` on every request, and transcript block compaction (max 60 blocks × 300 chars each per map chunk). |
| **Benchmark script** | `worker/benchmark.py` — run against a local video file to measure cold vs warm pipeline timing. See worker README for usage. |

## Client Preview

| Feature | Details |
|---------|----------|
| **Generated clip preview** | React clip thumbnails in campaign creation open a modal video preview on click. The client lazily calls `GET /api/fragments/{id}/clip-url`, which returns a presigned MinIO URL for the fragment `StorageKey`. Thumbnail images continue to use `GET /api/fragments/{id}/thumbnail-url`. |

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
