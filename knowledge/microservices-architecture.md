---
title: Microservices Architecture
tags:
  - architecture
  - microservices
  - kafka
  - gateway
created: 2026-05-10
---

# Microservices Architecture

ShoMark is split into a monorepo microservices architecture behind a Gateway BFF. The API source tree groups projects by service area under `api/src/{gateway,campaign,social,notification,shared}`.

## Services

| Service | Responsibility | Database |
|---------|----------------|----------|
| `ShoMark.Gateway` | YARP routing, Keycloak JWT validation, trusted user header injection, SSE proxying | none |
| `ShoMark.Services.Campaign.Api` | Videos, AI fragments, campaign lifecycle, worker completion events | `campaigns_db` |
| `ShoMark.Services.Social.Api` | Connected platforms, OAuth, posts, publishing, analytics | `social_db` |
| `ShoMark.Services.Notification.Api` | Notifications and notification SSE streams | `notifications_db` |

Each service keeps its own Domain / Application / Infrastructure / Api projects. Shared cross-service pieces live in:

```text
api/src/
  gateway/ShoMark.Gateway/
  campaign/ShoMark.Services.Campaign.{Api,Application,Domain,Infrastructure}/
  social/ShoMark.Services.Social.{Api,Application,Domain,Infrastructure}/
  notification/ShoMark.Services.Notification.{Api,Application,Domain,Infrastructure}/
  shared/ShoMark.{Common,Contracts,Messaging}/
```

| Project | Purpose |
|---------|---------|
| `ShoMark.Common` | Gateway trusted header names |
| `ShoMark.Contracts` | Kafka topic constants and domain event records |
| `ShoMark.Messaging` | Shared Kafka event publisher and Kafka options |

## Authentication

Only the gateway validates Keycloak JWTs. After validation it overwrites these downstream headers:

| Header | Value |
|--------|-------|
| `X-User-Id` | UUID from `sub` / name identifier claim |
| `X-User-Email` | Email claim when present |
| `X-User-Name` | Name or preferred username claim when present |

Downstream services trust these headers because they are internal Docker services and are not exposed on host ports. OAuth callback routes are anonymous so external platforms can redirect through the gateway into Social API.

## Kafka Events

| Topic | Producer | Consumers |
|-------|----------|-----------|
| `video-processing` | Campaign API | Python Worker |
| `video-processing-completed` | Python Worker | Campaign API |
| `fragment-approved` | Campaign API | Social API, Notification API |
| `campaign-status-changed` | Campaign API | Notification API |
| `post-published` | Social API | Notification API |
| `post-failed` | Social API | Notification API |

Social API stores approved fragment projections from `fragment-approved` and snapshots fragment media fields onto posts. Notification API creates persistent notifications from the domain events it consumes and pushes new notification DTOs over SSE.

## Docker Runtime

`docker/services/api/docker-compose.yaml` starts four .NET containers:

- `gateway` exposed on `${API_PORT}`
- `campaign-api` internal only
- `social-api` internal only
- `notification-api` internal only

PostgreSQL initialization creates `campaigns_db`, `social_db`, and `notifications_db` through `docker/infrastructure/postgres/init/01-create-service-databases.sql`.

Compose files provide local-development defaults for ports, credentials, Keycloak realm/client, Vite build args, Ollama model, and worker replica count. A `docker/.env` file or `--env-file` still overrides them, but plain `docker compose` commands from the `docker` directory no longer interpolate empty values.

The Kafka compose service creates `video-processing`, `video-processing-completed`, `fragment-approved`, `campaign-status-changed`, `post-published`, and `post-failed` during broker startup, then reports healthy only after those topics exist. APIs, worker replicas, and Kafka UI wait on Kafka health instead of a separate initializer container. Fresh PostgreSQL volumes also create the `keycloak` database alongside the service databases.

When the worker completion message has been persisted into AI fragments, Campaign API publishes `video-processing-succeeded`. Notification API consumes that domain event and creates a `VideoProcessingCompleted` notification for each user with a campaign attached to the processed video.

The Dockerized client runs Vite preview on port 3000. Vite keeps SPA fallback behavior for frontend routes and proxies `/api/*` to the Gateway at `http://gateway:8080`, so browser calls to `http://localhost:3000/api/...` reach the BFF instead of returning `index.html`.

Video uploads flow through the client proxy, Gateway, and Campaign API. Gateway Kestrel allows a 2 GiB video plus multipart envelope headroom, while Campaign API validates the actual file against `Video:MaxFileSizeBytes` and applies matching multipart request limits on `POST /api/videos/upload`.
