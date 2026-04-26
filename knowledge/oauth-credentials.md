---
title: OAuth Credentials Setup
tags:
  - oauth
  - setup
  - social-media
  - credentials
aliases:
  - OAuth Setup
  - Platform Credentials
created: 2026-04-25
---

# OAuth Credentials Setup

Step-by-step guide for obtaining `ClientId` and `ClientSecret` for each supported platform. After creating credentials, paste them into `appsettings.Development.json` (or environment variables in production) under the `OAuth` section.

---

## Instagram (Meta)

Instagram content publishing uses the **Facebook Graph API** through a Meta App.

### Prerequisites
- A Facebook account
- A Meta Business Portfolio (created automatically or via [business.facebook.com](https://business.facebook.com/))
- A Facebook Page linked to an Instagram Business or Creator account

### Steps

1. Go to [Meta for Developers](https://developers.facebook.com/) and log in.
2. Click **My Apps** → **Create App**.
3. Choose a **Business portfolio** to associate with the app (or create one if prompted).
4. Enter an **App name** (e.g., `ShoMark`) and a **contact email** → **Next**.
5. Select the **Other** use case → **Next**.
6. Select **Business** as the app type → **Create App**.
7. You will land on the **App Dashboard**.

**Add Facebook Login for Business:**

8. In the **left sidebar**, click the **`+` (Add Products)** button (or scroll to "Add products to your app" on the dashboard).
9. Find **Facebook Login for Business** → click **Set up**.
10. In the left sidebar, go to **Facebook Login for Business** → **Settings**:
    - Enable **Client OAuth Login** and **Web OAuth Login**.
    - Add the redirect URI: `http://localhost:5145/api/oauth/Instagram/callback`
    - Click **Save Changes**.

**Add Instagram Graph API and permissions:**

11. In the **left sidebar**, click **`+` (Add Products)** again.
12. Find **Instagram Graph API** → click **Set up**. It will appear in the left sidebar.
13. To add permissions, go to **App Review** → **Permissions and Features** in the left sidebar.
14. Search for and click **Request** (or **Add**) on each of the following:
    - `instagram_basic`
    - `instagram_content_publish`
    - `pages_show_list`
    - `pages_read_engagement`

> **Development Mode:** You do **not** need to submit for App Review. In Development Mode, these permissions are automatically available to users with a role on the app. Add test users under **App Roles** → **Roles**.

15. Go to **App Settings** → **Basic** in the left sidebar:
    - Copy the **App ID** — this is your `ClientId`.
    - Click **Show** next to **App Secret** — this is your `ClientSecret`.

---

### API Setup with Instagram Business Login (Current UI)

Once the Instagram Graph API product is added, Meta shows an **"API setup with Instagram business login"** page with 4 sections:

**Credentials (top of page):**
- **Instagram app name** — your app name (e.g., `ShoMark-IG`)
- **Instagram app ID** — shown here for reference, but **do NOT use this as `ClientId`**
- **Instagram app secret** — shown here for reference, but **do NOT use this as `ClientSecret`**

> ⚠️ **Use the Facebook App ID / Secret, not the Instagram App ID / Secret.**
> The `https://www.facebook.com/v21.0/dialog/oauth` endpoint validates the **Facebook App ID**
> (found under **App Settings → Basic**). The "Instagram app ID" shown on the product page
> is a different identifier and will trigger "Invalid App ID".

The correct values come from **App Settings → Basic**:
- **App ID** (top of page) → `ClientId`
- **App Secret** → click **Show** → `ClientSecret`

**Section 1 — Generate access tokens:**
- Click **Add account** to link an Instagram test account.
- Before this, make sure the Instagram account has the **Instagram Tester** role assigned under **App Roles** → **Roles**.

**Section 2 — Configure webhooks:** Skip — not required for ShoMark.

**Section 3 — Set up Instagram business login:**
- Click **Set up** to open the OAuth / redirect URI configuration.
- Add the redirect URI: `http://localhost:5145/api/oauth/Instagram/callback`
- Configure the required permissions: `instagram_basic`, `instagram_content_publish`, `pages_show_list`, `pages_read_engagement`

**Section 4 — Complete app review:** Only required for Live mode. In **Development Mode**, skip this step — permissions work for test users without review.

### Configuration

```json
"Instagram": {
  "ClientId": "<App ID>",
  "ClientSecret": "<App Secret>",
  "RedirectUri": "http://localhost:5145/api/oauth/Instagram/callback",
  "Scopes": "instagram_basic,instagram_content_publish,pages_show_list,pages_read_engagement"
}
```

---

## TikTok

Uses the **TikTok Login Kit** and **Content Posting API**.

### Prerequisites
- A TikTok account

### Steps

1. Go to the [TikTok for Developers](https://developers.tiktok.com/) portal and log in.
2. Click **Manage apps** → **Connect an app**.
3. Fill in app details (name, description, icon, category).
4. Under **Add products**, enable:
   - **Login Kit** — for OAuth
   - **Content Posting API** — for publishing videos
5. In **Login Kit** settings:
   - Set the **Redirect URI** — see note below about localhost.
   - Add the required scopes: `user.info.basic`, `video.publish`, `video.upload`
6. Submit the app for review (or use **Sandbox mode** for testing).
7. After approval, go to **Manage apps** → select your app:
   - Copy the **Client Key** — this is your `ClientId`.
   - Copy the **Client Secret** — this is your `ClientSecret`.

> **Sandbox mode** allows testing with up to 20 registered test users without full approval.

### ⚠️ Localhost Not Supported — Use ngrok for Local Testing

TikTok **does not accept `localhost`** as a redirect URI. To test locally, use **ngrok** to expose your local API via a public HTTPS URL:

1. Install ngrok:
   ```
   winget install ngrok
   ```
2. Start a tunnel on the API port:
   ```
   ngrok http 5145
   ```
3. Copy the generated URL (e.g., `https://xxxx-xxxx.ngrok-free.app`).
4. In the TikTok Developer Portal → your app → **Login Kit** settings, enter:
   ```
   https://xxxx-xxxx.ngrok-free.app/api/oauth/TikTok/callback
   ```
5. Update `appsettings.Development.json` with the same ngrok URL (see Configuration below).

> The ngrok URL **changes every session** on the free tier — you'll need to update both the TikTok portal and your config each time you restart ngrok. A paid ngrok plan provides a static domain.

### Configuration

```json
"TikTok": {
  "ClientId": "<Client Key>",
  "ClientSecret": "<Client Secret>",
  "RedirectUri": "https://<your-ngrok-subdomain>.ngrok-free.app/api/oauth/TikTok/callback",
  "Scopes": "user.info.basic,video.publish,video.upload"
}
```

---

## YouTube (Google)

Uses **Google OAuth 2.0** with the **YouTube Data API v3** for uploading Shorts.

### Prerequisites
- A Google account
- A YouTube channel

### Steps

1. Go to the [Google Cloud Console](https://console.cloud.google.com/).
2. Create a new project (or select an existing one).
3. Go to **APIs & Services** → **Library**.
4. Search for **YouTube Data API v3** → click **Enable**.
5. Go to **APIs & Services** → **Credentials** → **Create Credentials** → **OAuth client ID**.
6. If prompted, configure the **OAuth consent screen** first:
   - **User Type:** External (or Internal for Google Workspace accounts)
   - Fill in the app name, support email, and developer contact.
   - Add scopes: `youtube.upload`, `youtube.readonly`
   - Add any test users (required for External apps in testing mode).
   - Save and continue.
7. Back in **Create OAuth client ID**:
   - **Application type:** Web application
   - **Name:** `ShoMark`
   - **Authorized redirect URIs:** `http://localhost:5145/api/oauth/YouTube/callback`
   - Click **Create**.
8. Copy the **Client ID** and **Client Secret** from the confirmation dialog.

> **Testing mode** allows up to 100 test users without Google verification.

### Configuration

```json
"YouTube": {
  "ClientId": "<Client ID>",
  "ClientSecret": "<Client Secret>",
  "RedirectUri": "http://localhost:5145/api/oauth/YouTube/callback",
  "Scopes": "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly"
}
```

---

## X (Twitter)

Uses **X OAuth 2.0** with PKCE for the **X API v2**.

### Prerequisites
- An X (Twitter) account
- An X Developer account (free tier is sufficient)

### Steps

1. Go to the [X Developer Portal](https://developer.x.com/en/portal/dashboard) and sign in.
2. If you don't have a developer account, apply for one (choose **Free** or **Basic** tier).
3. In the Developer Portal, go to **Projects & Apps** → select your default project (or create one).
4. Click **Add App** (or edit the existing app within the project).
5. Go to the app's **Settings** tab:
   - Under **User authentication settings**, click **Set up**.
   - **App permissions:** Read and write
   - **Type of App:** Web App, Automated App or Bot
   - **Callback URI / Redirect URL:** `http://localhost:5145/api/oauth/X/callback`
   - **Website URL:** `http://localhost:5173` (or your domain)
   - Save.
6. Go to the **Keys and tokens** tab:
   - Under **OAuth 2.0 Client ID and Client Secret**:
     - Copy the **Client ID**.
     - Click **Regenerate** to view and copy the **Client Secret**.

> The **Free** tier allows 1,500 tweets/month and is sufficient for development. The **Basic** tier ($100/month) raises limits.

### Configuration

```json
"X": {
  "ClientId": "<Client ID>",
  "ClientSecret": "<Client Secret>",
  "RedirectUri": "http://localhost:5145/api/oauth/X/callback",
  "Scopes": "tweet.read tweet.write users.read offline.access"
}
```

---

## Production Checklist

- [ ] Replace all `localhost` redirect URIs with your production domain.
- [ ] Store `ClientSecret` values in environment variables or a secrets manager — never commit them to source control.
- [ ] Complete app review / verification on each platform before going live.
- [ ] Enable HTTPS for all redirect URIs in production (required by all platforms).
- [ ] Configure ASP.NET Data Protection key persistence for production (e.g., Azure Blob Storage, Redis, or a shared file path).

---

## See Also

- [[social-media-integration]] — Architecture overview of how these credentials are used in the publishing pipeline
- [[index]] — Knowledge base home
