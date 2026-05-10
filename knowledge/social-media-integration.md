---
title: Social Media Integration
tags:
  - oauth
  - publishing
  - architecture
  - social-media
aliases:
  - Publishing Pipeline
  - OAuth Architecture
created: 2026-04-25
updated: 2026-05-10
---

# Social Media Integration

ShoMark supports publishing content to **Instagram** (Reels), **TikTok**, **YouTube Shorts**, and **X (Twitter)**. Social publishing is owned by `ShoMark.Services.Social.Api` behind the Gateway BFF.

## Architecture Overview

```
React SPA ──JWT──► Gateway BFF ──X-User-Id──► Social API ──encrypted tokens──► social_db
                              │
Campaign API ──fragment-approved──► Social API fragment projection
                              │
Social API ──post-published/post-failed──► Notification API
```

1. **OAuth flow** — users connect accounts through Social API; tokens are encrypted at rest via ASP.NET Data Protection.
2. **Fragment projection** — Social API consumes `fragment-approved` events from Campaign API and stores approved fragment media metadata locally.
3. **Post creation** — posts reference `FragmentId`, but Social API snapshots fragment storage fields onto the post so publishing does not query Campaign DB.
4. **Scheduled publishing** — a `BackgroundService` polls every 30 seconds for due scheduled posts and calls `IPostPublishingService` inside Social API.
5. **Publishing events** — successful publishing emits `post-published`; failures emit `post-failed`. Notification API consumes both.

## Key Components

| Layer | Component | Purpose |
|-------|-----------|---------|
| Domain | `PostStatus.Publishing` | Publish-in-progress status |
| Domain | `FragmentProjection` | Local projection of approved Campaign fragments |
| Application | `IOAuthProvider` | Strategy interface — one implementation per platform |
| Application | `ISocialMediaPublisher` | Strategy interface for platform-specific publishing |
| Application | `ITokenEncryptionService` | Encrypt / decrypt platform tokens |
| Application | `IPostPublishingService` | Orchestrates publish flow and emits post events |
| Infrastructure | `FragmentApprovedConsumer` | Consumes `fragment-approved` into `social_db` |
| Infrastructure | `PostSchedulerBackgroundService` | 30-second polling scheduler |
| Api | `OAuthController` | `/api/oauth/{platform}/connect`, `callback`, `disconnect`, `refresh` |
| Api | `PostsController.Publish` | `POST /api/posts/{id}/publish` |

## OAuth Flow

1. Frontend calls `GET /api/oauth/{platform}/connect` through the gateway.
2. Social API generates a CSRF `state` token, stores it in `IMemoryCache`, and returns the platform authorization URL.
3. Frontend redirects the browser to the platform.
4. Platform redirects back to `GET /api/oauth/{platform}/callback?code=...&state=...` on the gateway.
5. The gateway allows callback routes anonymously and proxies them to Social API.
6. Social API validates state, exchanges the code for tokens, encrypts them, and creates/updates the `Platform` entity.
7. Social API redirects the browser to `/oauth/callback` on the frontend.

## Token Security

- Tokens are encrypted using `IDataProtector` with purpose string `"ShoMark.Tokens.v1"`.
- Gateway authentication is documented in [[keycloak]]. Downstream Social API trusts `X-User-Id` from the gateway.
- Tokens are only decrypted at publish time and during refresh; they are never returned in plain text to the frontend.
- If a token expires within 5 minutes, Social API refreshes it before publishing when a refresh token is available.

## Publishing Flow

1. `PostPublishingService` loads the post and platform from `social_db`.
2. Decrypts platform access/refresh tokens.
3. Refreshes the token if needed and persists the new encrypted tokens.
4. Generates a presigned MinIO URL from the post's fragment storage snapshot.
5. Resolves the correct `ISocialMediaPublisher` by `PlatformType`.
6. Calls the platform publisher with the access token, content, and media URL.
7. Updates the post to `Published` with `ExternalUrl`, or `Failed` on error.
8. Emits `post-published` or `post-failed` for Notification API.

## Kafka Topics

| Topic | Producer | Consumer |
|-------|----------|----------|
| `fragment-approved` | Campaign API | Social API, Notification API |
| `post-published` | Social API | Notification API |
| `post-failed` | Social API | Notification API |

## Configuration

OAuth settings live in `api/src/social/ShoMark.Services.Social.Api/appsettings.json` under the `OAuth` section. Local redirect URIs go through the gateway on port `5000`:

```json
{
  "OAuth": {
    "Instagram": {
      "ClientId": "",
      "ClientSecret": "",
      "RedirectUri": "http://localhost:5000/api/oauth/Instagram/callback",
      "Scopes": "instagram_basic,instagram_content_publish,pages_show_list,pages_read_engagement"
    }
  }
}
```

See [[oauth-credentials]] for instructions on obtaining platform credentials.

## Frontend

| File | Purpose |
|------|---------|
| `pages/SettingsPage.tsx` | Connected Accounts UI — connect / disconnect / status per platform |
| `pages/OAuthCallbackPage.tsx` | Handles redirect after OAuth, shows success/error |
| `api/platforms.ts` | `getConnectUrl`, `disconnect`, `refreshToken` API calls |
| `hooks/usePlatforms.ts` | `useConnectPlatform`, `useDisconnectPlatform`, `useRefreshPlatformToken` |
| `api/posts.ts` | `publish(id)` — immediate publish |
| `hooks/usePosts.ts` | `usePublishPost` hook |

---

## See Also

- [[microservices-architecture]] — service boundaries and Kafka events
- [[oauth-credentials]] — Step-by-step guide to obtaining platform credentials
- [[keycloak]] — JWT authentication and gateway trusted headers
- [[index]] — Knowledge base home
