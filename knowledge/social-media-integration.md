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
---

# Social Media Integration

ShoMark supports publishing content to **Instagram** (Reels), **TikTok**, **YouTube Shorts**, and **X (Twitter)**. This document explains the architecture and how each piece fits together.

## Architecture Overview

```
┌─────────────┐   OAuth    ┌──────────────────┐   Encrypted   ┌────────────┐
│  React SPA  │  ───────►  │  OAuthController │  ──────────►  │ PostgreSQL │
└─────────────┘            └──────────────────┘               └────────────┘
                                                               │
┌─────────────┐   Kafka    ┌──────────────────┐   Decrypt +    │
│  Scheduler  │  ───────►  │  Publishing      │   Publish      ▼
│  (30s poll) │           │  Consumer         │  ──────────►  Social APIs
└─────────────┘            └──────────────────┘
```

1. **OAuth flow** — user connects accounts from the Settings page; tokens are encrypted at rest via ASP.NET Data Protection.
2. **Scheduled publishing** — a `BackgroundService` polls every 30 seconds for posts where `ScheduledAt <= now`, moves them to `Publishing` status, and produces a message to the `post-publishing` Kafka topic.
3. **Publish consumer** — a Kafka consumer picks up the message, decrypts tokens, auto-refreshes if expired, fetches a presigned media URL from MinIO, and calls the platform-specific publisher.
4. **Immediate publishing** — the `POST /api/posts/{id}/publish` endpoint triggers the same flow synchronously.

## Key Components

| Layer | Component | Purpose |
|-------|-----------|---------|
| Domain | `PostStatus.Publishing` | New enum value (4) indicating publish-in-progress |
| Application | `IOAuthProvider` | Strategy interface — one implementation per platform |
| Application | `ISocialMediaPublisher` | Strategy interface for publishing content per platform |
| Application | `ITokenEncryptionService` | Encrypt / decrypt tokens (Data Protection) |
| Application | `IPostPublishingService` | Orchestrates the full publish flow |
| Application | `IPostPublishingProducer` | Produces post IDs to Kafka |
| Infrastructure | `InstagramOAuthProvider` | Meta Graph API OAuth (short → long-lived token) |
| Infrastructure | `TikTokOAuthProvider` | TikTok Login Kit v2 |
| Infrastructure | `YouTubeOAuthProvider` | Google OAuth2 (youtube.upload scope) |
| Infrastructure | `XOAuthProvider` | X OAuth 2.0 with PKCE |
| Infrastructure | `InstagramPublisher` | Graph API Reels (container → poll → publish) |
| Infrastructure | `TikTokPublisher` | Content Posting API (URL pull) |
| Infrastructure | `YouTubePublisher` | Data API v3 resumable upload |
| Infrastructure | `XPublisher` | v2 tweets + chunked media upload |
| Infrastructure | `PostSchedulerBackgroundService` | 30-second polling scheduler |
| Infrastructure | `KafkaPostPublishingConsumer` | Consumes `post-publishing` topic |
| Api | `OAuthController` | `/api/oauth/{platform}/connect`, `callback`, `disconnect`, `refresh` |
| Api | `PostsController.Publish` | `POST /api/posts/{id}/publish` |

## OAuth Flow (Step by Step)

1. Frontend calls `GET /api/oauth/{platform}/connect`.
2. Controller generates a CSRF `state` token, stores it in `IMemoryCache`, and returns the platform's authorization URL.
3. Frontend redirects the browser to the authorization URL.
4. User logs in on the platform and grants permissions.
5. Platform redirects back to `GET /api/oauth/{platform}/callback?code=...&state=...`.
6. Controller validates state, exchanges the code for tokens, encrypts them, and creates/updates the `Platform` entity.
7. Controller redirects the browser to `/oauth/callback` on the frontend, which shows a success/error message and navigates to Settings.

## Token Security

- Tokens are encrypted using `IDataProtector` with purpose string `"ShoMark.Tokens.v1"`. Authentication is handled by [[keycloak]] (JWT Bearer).
- Keys are stored via the default ASP.NET Data Protection key ring (`PersistKeysToFileSystem` or similar provider should be configured for production).
- Tokens are only decrypted at publish time and during refresh — they are never returned in plain text to the frontend.
- Auto-refresh: if a token expires within 5 minutes, the system automatically refreshes it before publishing.

## Publishing Flow

1. `PostPublishingService` loads the post and its associated platform.
2. Decrypts access/refresh tokens.
3. Checks expiry — refreshes if needed and persists the new encrypted tokens.
4. Generates a presigned MinIO URL for the post's media (video fragment).
5. Resolves the correct `ISocialMediaPublisher` by `PlatformType`.
6. Calls `PublishPostAsync` with the access token, content, and media URL.
7. Updates the post with `ExternalUrl`, `ExternalPostId`, and `Published` status, or `Failed` on error.

## Kafka Topics

| Topic | Producer | Consumer |
|-------|----------|----------|
| `post-publishing` | `PostSchedulerBackgroundService` / immediate publish | `KafkaPostPublishingConsumer` |

## Configuration

All OAuth settings live in `appsettings.json` under the `OAuth` section:

```json
{
  "OAuth": {
    "Instagram": {
      "ClientId": "",
      "ClientSecret": "",
      "RedirectUri": "http://localhost:5145/api/oauth/Instagram/callback",
      "Scopes": "instagram_basic,instagram_content_publish,pages_show_list,pages_read_engagement"
    },
    "TikTok": {
      "ClientId": "",
      "ClientSecret": "",
      "RedirectUri": "http://localhost:5145/api/oauth/TikTok/callback",
      "Scopes": "user.info.basic,video.publish,video.upload"
    },
    "YouTube": {
      "ClientId": "",
      "ClientSecret": "",
      "RedirectUri": "http://localhost:5145/api/oauth/YouTube/callback",
      "Scopes": "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly"
    },
    "X": {
      "ClientId": "",
      "ClientSecret": "",
      "RedirectUri": "http://localhost:5145/api/oauth/X/callback",
      "Scopes": "tweet.read tweet.write users.read offline.access"
    }
  }
}
```

See [[oauth-credentials]] for instructions on obtaining `ClientId` and `ClientSecret` for each platform.

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

- [[oauth-credentials]] — Step-by-step guide to obtaining platform credentials
- [[keycloak]] — JWT authentication used to protect all API endpoints
- [[index]] — Knowledge base home
