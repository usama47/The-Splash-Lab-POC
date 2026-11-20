# Deploy / publish guide — The Splash Lab POC

This POC has **two apps** that both need to be reachable for a client demo:

| App | Stack | Local default |
|---|---|---|
| API | ASP.NET Core 8 | http://localhost:5080 |
| UI | Next.js 14 | http://localhost:3000 |

Cursor `publish-app` hosting is a weak fit for the .NET API. Use one of the options below.

---

## Before any public deploy

1. Push this repo to GitHub (you already have `https://github.com/usama47/The-Splash-Lab-POC.git`).
2. Decide which path you want:
   - **A — Same-day share:** local + tunnel
   - **B — Shareable for a week:** Render/Railway API + Vercel UI
   - **C — One box:** Docker Compose on a VPS
   - **D — Microsoft-shaped:** Azure App Service (+ Static Web Apps)
3. Set UI → API URL:
   ```bash
   NEXT_PUBLIC_API_BASE=https://YOUR-PUBLIC-API-URL
   ```
4. Allow the public UI origin in API CORS via config (see below). Do **not** leave CORS wide open in production-like demos without understanding the risk.

### API CORS for a public UI

`appsettings.json` / environment:

```json
"Cors": {
  "AllowedOrigins": [
    "http://localhost:3000",
    "http://127.0.0.1:3000",
    "https://YOUR-UI-HOST"
  ]
}
```

Or env:

```bash
Cors__AllowedOrigins__0=http://localhost:3000
Cors__AllowedOrigins__1=https://YOUR-UI-HOST
```

### API SQLite persistence

Default DB file:

`backend/src/TheSplashLab.Api/Data/splashlab-poc.db`

On cloud hosts, mount a **persistent volume** and set:

```bash
Database__Path=/data/splashlab-poc.db
```

Without a volume, the DB (and demo data) can reset when the service redeploys.

### Keep MODE A for demos

```bash
Ai__Provider=Mock
# leave ANTHROPIC_API_KEY unset unless intentionally enabling Claude
```

---

## Option A — Fastest: local run + tunnel

Good for a same-day client walkthrough.

### 1. Run API

```bash
cd backend/src/TheSplashLab.Api
dotnet run --urls http://0.0.0.0:5080
```

### 2. Tunnel the API

Example with Cloudflare quick tunnel (any equivalent tunnel tool works):

```bash
cloudflared tunnel --url http://localhost:5080
# note the https://....trycloudflare.com URL
```

### 3. Run UI pointed at the public API

```bash
cd frontend
NEXT_PUBLIC_API_BASE=https://YOUR-API-TUNNEL-URL npm run dev -- -p 3000
```

### 4. Tunnel the UI

```bash
cloudflared tunnel --url http://localhost:3000
```

Share the **UI** tunnel URL with the client.

Also add the UI tunnel origin to `Cors:AllowedOrigins`, then restart the API.

**Pros:** fastest. **Cons:** needs your machine online; URLs change unless you use a named tunnel.

---

## Option B — Recommended hosted split (Render/Railway + Vercel)

Good for a stable demo link for several days.

### B1. Deploy API (Render example)

1. New **Web Service** from this GitHub repo.
2. Root / Dockerfile: use the API Dockerfile in this repo (`deploy/api.Dockerfile`) **or** native .NET build:
   - **Runtime:** Docker, or Render’s Docker build from `deploy/api.Dockerfile`
   - **Health check:** `/api/health`
3. Env:
   ```bash
   ASPNETCORE_ENVIRONMENT=Production
   ASPNETCORE_URLS=http://0.0.0.0:8080
   Ai__Provider=Mock
   Database__Path=/data/splashlab-poc.db
   Cors__AllowedOrigins__0=https://YOUR-VERCEL-APP.vercel.app
   ```
4. Attach a **persistent disk** mounted at `/data`.
5. Note the public API URL, e.g. `https://splashlab-api.onrender.com`.

Railway / Fly.io follow the same idea: one service, volume for `/data`, public HTTPS URL.

### B2. Deploy UI (Vercel example)

1. Import the GitHub repo in Vercel.
2. **Root Directory:** `frontend`
3. Framework: Next.js (auto).
4. Env:
   ```bash
   NEXT_PUBLIC_API_BASE=https://splashlab-api.onrender.com
   ```
5. Deploy. Note the UI URL.
6. Put that UI URL into the API CORS list and redeploy the API if needed.

**Pros:** easy, HTTPS, shareable. **Cons:** two services to manage; SQLite needs a disk on the API host.

---

## Option C — One VPS with Docker Compose

Best when you want a single domain story.

Files in this repo:

- `deploy/api.Dockerfile`
- `deploy/web.Dockerfile`
- `deploy/docker-compose.yml`
- `deploy/.env.example`

### Steps

1. Provision a small Ubuntu VPS (DigitalOcean, Hetzner, etc.).
2. Install Docker + Docker Compose.
3. Clone the repo on the VPS.
4. Copy env:
   ```bash
   cd deploy
   cp .env.example .env
   # edit PUBLIC_API_URL / PUBLIC_UI_ORIGIN / domains as needed
   ```
5. Start:
   ```bash
   docker compose up -d --build
   ```
6. Put Caddy/Nginx (or a cloud load balancer) in front for HTTPS, or use the compose Caddy profile if you add one later.

Compose exposes:

- API on host port `5080` (container `8080`)
- UI on host port `3000`

Point DNS / reverse proxy:

- `https://api.yourdomain.com` → API
- `https://demo.yourdomain.com` → UI

Set:

```bash
NEXT_PUBLIC_API_BASE=https://api.yourdomain.com
Cors__AllowedOrigins__0=https://demo.yourdomain.com
```

**Pros:** one place, durable SQLite volume, client-friendly domains. **Cons:** you manage the server.

---

## Option D — Azure (Microsoft-aligned)

1. **API:** Azure App Service (.NET 8)
   - Deploy from GitHub Actions or `az webapp deploy`
   - Configure `Database__Path` on persistent storage, or accept reseed on recycle for short demos
   - Set `Cors__AllowedOrigins`
2. **UI:** Azure Static Web Apps **or** a second App Service for Node
   - Set `NEXT_PUBLIC_API_BASE` to the App Service API URL

Keep MODE A (`Ai__Provider=Mock`) unless you intentionally enable Claude with Key Vault.

---

## Smoke checklist after deploy

1. Open UI URL → Dashboard loads counts (not a CORS/network error). Re-check this before every demo: `NEXT_PUBLIC_API_BASE` and `Cors:AllowedOrigins` must match the current UI/API URLs (tunnel URLs change).
2. Open `/api/health` on the API → `{ "status": "ok", ... }`.
3. Meetings list shows seeded scenarios.
4. Generate Draft → Save → Publish on the UK sample meeting.
5. Decisions / Commitments / Audit update.
6. Restart API once and confirm records still exist (volume working).

---

## Security notes for demos

- Demo identity switching is **not** Microsoft Entra auth.
- Do not put real Anthropic / Graph secrets in the frontend.
- Prefer private/password-protected share links when the host supports it.
- This is a **POC**, not a production security certification.

---

## Quick decision guide

| Need | Choose |
|---|---|
| Demo in the next hour | **A — tunnel** |
| Stable link for a client this week | **B — Render/Railway + Vercel** |
| One domain you control | **C — Docker Compose VPS** |
| Client already on Azure | **D — Azure** |
