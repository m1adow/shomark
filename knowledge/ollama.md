---
title: Ollama Configuration Guide
tags:
  - ai
  - llm
  - setup
  - infrastructure
aliases:
  - Ollama Setup
  - LLM Config
created: 2026-04-25
---

# Ollama Configuration Guide

Ollama runs locally as a containerized LLM inference server. The worker service uses it to generate marketing text via the `/api/generate` endpoint.

## 1. Start the Container

```bash
cd docker/infrastructure/ollama
docker compose up -d
```

The container exposes port **11434** and persists model data in the `ollama_data` Docker volume.

**With GPU (NVIDIA):** the compose file already configures GPU reservation — no changes needed, just ensure the [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html) is installed on the host.

**CPU only:** remove or comment out the `deploy.resources` block in `docker/infrastructure/ollama/docker-compose.yaml` before starting.

## 2. Install a Model

After the container is running, pull a model into it:

```bash
docker exec -it ollama ollama pull gemma4:e4b
```

Replace `gemma4:e4b` with any model from [ollama.com/library](https://ollama.com/library).

To verify the model was installed:

```bash
docker exec -it ollama ollama list
```

To remove a model:

```bash
docker exec -it ollama ollama rm gemma4:e4b
```

## 3. Configure the Worker

The worker reads these environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_URL` | `http://ollama:11434/api/generate` | Ollama generate endpoint (inside Docker network) |
| `OLLAMA_MODEL` | `gemma4:e4b` | Model name passed in every request |
| `OLLAMA_TIMEOUT` | `300` | HTTP timeout in seconds for each Ollama request |
| `OLLAMA_NUM_PREDICT` | `1024` | Max tokens Ollama may generate per response (limits runaway generation) |

Set `OLLAMA_MODEL` in your `.env` file (used by `docker/services/docker-compose.yaml`):

```env
OLLAMA_MODEL=gemma4:e4b
```

To use a different model, pull it first (step 2) and then update `OLLAMA_MODEL`.

**Performance note:** The worker reuses a single HTTP session across all Ollama requests and sets `format: "json"` on every request so the model skips markdown code-block wrapping, reducing parse overhead. The `OLLAMA_NUM_PREDICT` cap prevents the model from generating thousands of tokens when a short JSON array is expected.

## 4. Test the API Directly

From the host machine:

```bash
curl http://localhost:11434/api/generate \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gemma4:e4b",
    "prompt": "Write a short Instagram post about a career fair.",
    "stream": false
  }'
```

Expected response contains a `"response"` field with the generated text.

## 5. Configuration Reference

| Setting | Value |
|---------|-------|
| Container name | `ollama` |
| API port | `11434` |
| Generate endpoint | `http://localhost:11434/api/generate` |
| Model storage volume | `ollama_data` → `/root/.ollama` |
| Default model | `gemma4:e4b` |
| Compose file | `docker/infrastructure/ollama/docker-compose.yaml` |
| Request format | `format: "json"` (enforced by worker — valid JSON output, no markdown wrapping) |
| Max output tokens | `OLLAMA_NUM_PREDICT=1024` (configurable) |
| HTTP timeout | `OLLAMA_TIMEOUT=300` seconds (configurable) |
