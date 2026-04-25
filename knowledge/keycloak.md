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

## 6. Configuration Reference

| Setting | Value |
|---------|-------|
| Admin URL | http://localhost:8180 |
| Realm | `shomark` |
| Client ID | `shomark-api` |
| Token endpoint | `http://localhost:8180/realms/shomark/protocol/openid-connect/token` |
| JWKS endpoint | `http://localhost:8180/realms/shomark/protocol/openid-connect/certs` |
| API audience (appsettings) | `shomark-api` |
| API authority (appsettings) | `http://keycloak:8080/realms/shomark` (docker) / `http://localhost:8180/realms/shomark` (dev) |

---

## See Also

- [[social-media-integration]] — OAuth flow and publishing pipeline that uses these JWT tokens
- [[index]] — Knowledge base home