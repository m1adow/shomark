---
title: Keycloak Configuration Guide
tags:
  - auth
  - setup
  - infrastructure
aliases:
  - Keycloak Setup
  - JWT Config
created: 2026-04-25
---

# Keycloak Configuration Guide

## 1. Access Admin Console

Open http://localhost:8180 and log in with **admin / admin**.

## 2. Create Realm

1. Click the dropdown in the top-left (shows "master")
2. Click **Create realm**
3. Set **Realm name** to `shomark`
4. Click **Create**

## 3. Create Client

1. Go to **Clients** → **Create client**
2. **General settings:**
   - **Client type:** OpenID Connect
   - **Client ID:** `shomark-api`
   - Click **Next**
3. **Capability config:**
   - **Client authentication:** OFF (public client — the React frontend will request tokens directly)
   - **Authorization:** OFF
   - **Authentication flow** — enable only:
     - [x] Standard flow (Authorization Code — used by the React SPA)
     - [x] Direct access grants (Resource Owner Password — useful for testing via Scalar/Postman)
     - [ ] Implicit flow — leave OFF (deprecated, not secure)
     - [ ] Service accounts roles — leave OFF (no backend-to-backend auth needed)
   - Click **Next**
4. **Login settings:**
   - **Root URL:** `http://localhost:5173` (React dev server)
   - **Home URL:** `http://localhost:5173`
   - **Valid redirect URIs:** `http://localhost:5173/*`
   - **Valid post logout redirect URIs:** `http://localhost:5173/*`
   - **Web origins:** `http://localhost:5173` (enables CORS for token requests)
   - Click **Save**

## 4. Create a Test User

1. Go to **Users** → **Add user**
2. Set **Username**, **Email**, **First name**, **Last name**
3. Click **Create**
4. Go to the **Credentials** tab → **Set password**
5. Enter a password, set **Temporary** to OFF
6. Click **Save**

## 5. Get a Token (Testing)

```bash
curl -X POST http://localhost:8180/realms/shomark/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=shomark-api" \
  -d "username=YOUR_USERNAME" \
  -d "password=YOUR_PASSWORD"
```

Copy the `access_token` from the response and use it in the Scalar UI (**Bearer** auth) or in requests:

```
Authorization: Bearer <access_token>
```

## 6. Gateway BFF Auth Flow

ShoMark now validates JWTs only in `ShoMark.Gateway`. The gateway proxies `/api/**` with YARP and overwrites trusted internal headers before forwarding to downstream APIs:

| Header | Source |
|--------|--------|
| `X-User-Id` | `sub` / name identifier claim |
| `X-User-Email` | `email` claim |
| `X-User-Name` | `name`, `preferred_username`, or name claim |

Campaign, Social, and Notification APIs trust these headers on the Docker network and do not validate JWTs themselves. Their containers are internal only; only `gateway` is published on `${API_PORT}`.

SSE endpoints still work with browser `EventSource`: the gateway accepts `access_token` query parameters for `/api/videos/{id}/events` and `/api/notifications/stream`, validates the token, then forwards trusted headers downstream.

OAuth platform callbacks under `/api/oauth/{platform}/callback` are anonymous at the gateway so external providers can complete the redirect into Social API.

## 7. Configuration Reference

| Setting | Value |
|---------|-------|
| Admin URL | http://localhost:8180 |
| Realm | `shomark` |
| Client ID | `shomark-api` |
| Token endpoint | `http://localhost:8180/realms/shomark/protocol/openid-connect/token` |
| JWKS endpoint | `http://localhost:8180/realms/shomark/protocol/openid-connect/certs` |
| API audience (appsettings) | `shomark-api` |
| Gateway authority (appsettings) | `http://keycloak:8080/realms/shomark` (docker) / `http://localhost:8180/realms/shomark` (dev) |

---

## See Also

- [[social-media-integration]] — OAuth flow and publishing pipeline that uses these JWT tokens
- [[microservices-architecture]] — gateway, service boundaries, and trusted headers
- [[index]] — Knowledge base home