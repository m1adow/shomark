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

## 6. Highlight-Detection Prompt Rubric

The worker has two prompt branches in `worker/highlight_finder.py`:

- **Map-Reduce** (`_build_map_prompt` + `_build_reduce_prompt`) — runs when the user does **not** supply a `description`. Splits the transcript into `MAP_CHUNKS` parallel chunks, the LLM finds 2 candidates per chunk, then a reduce step picks the top `TOP_HIGHLIGHTS`.
- **User-Instruction** (`_find_highlights_two_pass`) — runs when the user supplies a `description` (e.g. "make a clip per profession"). One LLM call over the full transcript, with a step-1 mental entity list and `_anchor` self-citation.

Both prompts enforce the same quality rubric:

### Output schema (unchanged across both branches)

```json
{
  "start": 1936,
  "end": 2037,
  "title": "Стипендія 8000 грн для першокурсників ФІТ",
  "reason": "Приваблює абітурієнтів конкретною сумою фінансової підтримки на старті.",
  "viral_score": 0.9,
  "hashtags": "#ФІТ #стипендія8000 #першокурсник #абітурієнт2026"
}
```

### Field-quality rules baked into the prompts

| Field | Rule |
|---|---|
| `start` | Must equal a real segment's start time (no arithmetic). For user-instruction branch: validated post-hoc via `_anchor` substring match against the transcript line at that timestamp; mismatched clips are dropped. |
| `end` | Must equal a real segment's end time. Duration `end - start` ∈ `[clip_duration − 10, clip_duration + 15]` for map-reduce; `[30, 180]` for user-instruction. |
| `title` | Ukrainian noun phrase ≤ 8 words, must reference a concrete fact (number, proper noun, programme, technology). Banned regex: `^(цікав|захопл|важлив|корисн|ключов)\s`. |
| `reason` | 1–2 Ukrainian sentences, must start with an audience-benefit verb (`Приваблює…`, `Демонструє…`, `Мотивує…`) and cite a concrete fact from the clip. |
| `viral_score` | Anchored rubric: **0.9+** named achievement / concrete number / proper noun · **0.7** emotional but generic · **0.5** informative but flat · **<0.5** filler. Score spread across the final set is required (no flat 0.8 bias). |
| `hashtags` | 3–5 Ukrainian hashtags. **At least 2** must be proper nouns from the clip (technology, programme, name). Pure-generic sets like `#навчання #університет #освіта` are banned. |

### Diversity & gap rules

- **Map-Reduce reduce step**: no two final clips may share the title's first noun (forces topical variety).
- **User-Instruction**: minimum 30 s gap between any two `start` values, enforced both inside the prompt and post-hoc; near-duplicates resolve to the higher `viral_score`.
- **Overlap trim**: after sorting by `start`, each clip's `end` is capped at the next clip's `start`; clips shorter than 30 s after trimming are dropped.

### `_anchor` self-citation (user-instruction branch only)

The LLM must include an internal `_anchor` field — the exact `[mm:ss] line text…` from the transcript at the clip's `start`. The worker validates that the anchor's text is a substring (case-insensitive, first 40 chars) of the actual transcript line at that timestamp; clips that fail the check are discarded. The field is stripped from the final result so it never reaches downstream consumers.

### Eval harness

Located at `worker/tests/test_prompt_eval.py` with golden fixtures under `worker/tests/prompts/*.json`. Skipped unless `OLLAMA_URL` is set, so it stays out of normal CI:

```powershell
$env:OLLAMA_URL="http://localhost:11434/api/generate"
$env:OLLAMA_MODEL="gemma4:e4b"
$env:KAFKA_BOOTSTRAP_SERVERS="dummy"
$env:MINIO_ENDPOINT="dummy"; $env:MINIO_ACCESS_KEY="dummy"; $env:MINIO_SECRET_KEY="dummy"
pytest worker/tests/test_prompt_eval.py -v -s
```

Each fixture asserts: count window, `start` grounded ±2 s to a real segment, no overlaps, no `start` below the configured floor, Cyrillic-only fields, no banned-boilerplate titles, `viral_score` spread, and entity coverage where applicable.
