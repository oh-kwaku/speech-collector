# Speech Collector

Data collection app for capturing children's (ages 3-12) speech: a photo is shown,
the child describes it, a **Collector** records the audio, an **Annotator**
transcribes it, and an **Admin** manages users and exports metadata for the ML
pipeline. Speakers are identified only by a generated `speakerId` + gender + age —
no names or other PII are ever collected.

## Stack

- `backend/` — ASP.NET Core 8 Web API, EF Core (Postgres), JWT auth, S3 pre-signed
  URLs, Gmail SMTP for invite/OTP email.
- `frontend/` — React + TypeScript + Vite + Tailwind, mobile-first collector UI.

## Local development

`docker-compose.yml` at the repo root supports two ways to run this locally:

**Option A — backing services only** (default, best for active development —
hot reload, debugger attach). No `.env` file required:

```bash
docker compose up -d
```

That gives you Postgres on `localhost:5432` and a MinIO S3-compatible store on
`localhost:9000` (API) / `localhost:9001` (web console, login
`recordingapp` / `recordingapp-dev-secret`), with the `photos`/`audio` buckets
already created. The API and frontend themselves run natively, not in Docker:

**Backend** (needs the .NET 8 SDK):

```bash
cd backend
cp src/RecordingApp.Api/appsettings.json src/RecordingApp.Api/appsettings.Development.json # edit as needed, or use user-secrets
dotnet ef database update --project src/RecordingApp.Infrastructure --startup-project src/RecordingApp.Api
dotnet run --project src/RecordingApp.Api
```

Fill in `ConnectionStrings:Postgres`, `Jwt:SigningKey`, `S3:*`, and `Smtp:*` in
`appsettings.Development.json` (or via `dotnet user-secrets` / environment
variables — never commit real secrets). Point `S3:ServiceUrl` at
`http://localhost:9000` with `S3:ForcePathStyle: true` and
`S3:AccessKey`/`S3:SecretKey` set to `recordingapp` /
`recordingapp-dev-secret` (or whatever you overrode `MINIO_ROOT_USER`/
`MINIO_ROOT_PASSWORD` to). S3 storage is provider-agnostic in general — the
same settings also work against AWS S3, DigitalOcean Spaces, Cloudflare R2,
Backblaze B2, Wasabi, etc., or leave `ServiceUrl` blank and set `S3:Region` to
talk to AWS S3 directly (falling back to the standard AWS credential chain —
env vars, shared config, or an instance role — when `AccessKey`/`SecretKey`
are blank).

**Frontend**:

```bash
cd frontend
cp .env.example .env   # points VITE_API_BASE_URL at the API
npm install
npm run dev
```

**Option B — the whole app containerized** (Postgres, MinIO, API, web), for
when you just want the app running rather than to edit it:

```bash
docker compose --profile full up -d --build
```

This builds and runs everything, including the API/frontend Dockerfiles used
for production, at `http://localhost:8081`. It also seeds a dev-only Admin
(phone `+10000000000`, works out of the box since SMS is stubbed to the
console per project decision) so you can log in immediately — read the OTP
code from `docker compose logs -f api`. Override `BOOTSTRAP_ADMIN_EMAIL` /
`BOOTSTRAP_ADMIN_PHONE` (and any other var in `.env.example`) via a root
`.env` file to customize. The `api` service uses Docker's host networking so
its pre-signed S3 URLs resolve the same way for the container and your
browser — Linux only; on Mac/Windows use Option A instead.

## Tests

```bash
cd backend
dotnet test
```

## Deploying to production (Dokploy)

Production is deployed as **separate Dokploy applications**, not as one
Docker Compose stack:

- `web` — a Dokploy "Application" (Dockerfile) service built from
  `frontend/Dockerfile`.
- `api` — a Dokploy "Application" (Dockerfile) service built from
  `backend/Dockerfile`.
- Postgres — provisioned via Dokploy's built-in Database feature (not this
  repo's `docker-compose.yml` Postgres).
- MinIO (the default S3-compatible object store) — its own standalone
  Dokploy app.

No Caddy anywhere: Dokploy runs its own Traefik instance and handles
HTTPS/domain routing per app from its dashboard.

`VITE_API_BASE_URL` (the frontend's API URL) is read by the `web` container
at **startup**, not baked into the JS bundle at build time: its nginx image
regenerates a small `env-config.js` from the container's real environment
before nginx starts (see `frontend/docker-entrypoint.d/env-config.sh`), and
`frontend/src/api/client.ts` reads that at runtime in preference to the
build-time value. So changing it in Dokploy's Environment tab just needs a
restart of `web`, not a rebuild. The `ARG VITE_API_BASE_URL=/api` in
`frontend/Dockerfile` only sets the fallback default baked in for
`vite build`/`vite preview` without Docker (and for the GitHub Pages build
below, which has no running container at all).

Steps, per app:

1. In Dokploy, create an Application pointed at this repo for `web`
   (Dockerfile path `frontend/Dockerfile`, build context `frontend/`) and
   another for `api` (Dockerfile path `backend/Dockerfile`, build context
   `backend/`).
2. On `web`, set `VITE_API_BASE_URL` under **Environment Variables** — e.g.
   `/api` if `api` is routed under the same domain at path `/api`, or
   `https://api.yourdomain.com` if it's on its own subdomain.
3. On `api`, fill in its runtime config (connection strings, JWT signing
   key, S3 credentials, SMTP, bootstrap admin, etc. — see `.env.example` for
   the full list of values needed) under Environment Variables.
4. Provision Postgres via Dokploy's Database feature and point `api`'s
   `ConnectionStrings__Postgres` at it.
5. Deploy MinIO as its own Dokploy app (or point at real AWS S3/another
   provider instead by setting the `S3_*` vars on `api` and skipping MinIO
   entirely).
6. Add Domains in Dokploy for:
   - `web`, container port 80 → your main app domain, path `/`
   - `api`, container port 8080 → same domain, path `/api` (or a dedicated
     `api.*` subdomain)
   - `minio`, container port 9000 → a dedicated subdomain (e.g.
     `s3.yourdomain.com`) — this must be reachable by the *browser*, since
     the API hands out pre-signed URLs against `S3_SERVICE_URL` directly to
     it. Leave the MinIO console (port 9001) without a domain; it's not
     meant to be public — reach it via `docker exec`/an SSH tunnel instead.
   (Exact steps depend on your Dokploy version's UI — check its current
   docs.)

`docker-compose.dokploy.yml` still exists as an alternative single-stack
Compose deployment (Postgres, MinIO, API, web all in one Dokploy "Docker
Compose" application) if you'd rather not manage separate apps — see the
comments at the top of that file. The same runtime `env-config.js` behavior
applies there: `VITE_API_BASE_URL` set on the compose service's environment
takes effect on restart, no rebuild needed.

## Deploying the frontend to GitHub Pages

The frontend can also be published as a static site on GitHub Pages,
independent of the Dokploy deployment above — useful for a demo/staging
frontend against a backend API that's already deployed elsewhere. GitHub
Pages only serves static files: there's no server to run the runtime
`env-config.js` trick the Docker image uses, and no `/api` reverse proxy, so
the API's full URL has to be baked into the build instead.

1. One-time GitHub setup: in the repo's Settings → Pages, set **Source** to
   "Deploy from a branch", branch `gh-pages`, folder `/(root)`. (The first
   `npm run deploy` below creates that branch — you can only select it here
   afterwards.)
2. Make sure a real backend API is already deployed and reachable over HTTPS
   from the browser (e.g. via Dokploy, above), and that it allows the GitHub
   Pages origin in CORS: set `Cors__AllowedOrigins__0=https://<owner>.github.io`
   in the API's environment (add `Cors__AllowedOrigins__1`, etc. for
   additional origins).
3. From `frontend/`, build and publish with that API URL baked in:

   ```bash
   cd frontend
   VITE_API_BASE_URL=https://api.yourdomain.com npm run deploy
   ```

   `npm run deploy` (the `gh-pages` package) runs `npm run build`
   (`predeploy`) and pushes the resulting `dist/` to the `gh-pages` branch.
4. The site is served at `https://<owner>.github.io/speech-collector/` —
   `vite.config.ts`'s `base: '/speech-collector/'` must match the repo name,
   since GitHub Pages project sites are served from that subpath. Update it
   if the repo is ever renamed.

Client-side routes (e.g. `/dashboard`) still work on refresh/direct link even
though GitHub Pages has no server-side rewrite rules, via a small redirect
trick in `public/404.html` + `index.html` (see the comments there).

## First login (no default admin)

There is no password login and no seeded default admin — auth is OTP-only
(email/SMS code), and inviting a user requires an existing Admin. To break that
chicken-and-egg problem, the API seeds exactly one Admin on startup **if none
exists yet**, from `Bootstrap:AdminEmail` / `Bootstrap:AdminPhoneNumber`
(`BOOTSTRAP_ADMIN_EMAIL` / `BOOTSTRAP_ADMIN_PHONE`, set via Dokploy's
Environment tab in production).
Set one of those, start the app, then sign in with the normal "request a code"
flow using that email/phone — from there, invite everyone else as Admin/
Collector/Annotator through the Users page. Safe to leave blank on every
startup after that first Admin exists.

## Notes

- **Anonymization**: speakers have no name/DOB/contact fields anywhere in the
  schema — only `speakerId`, gender, age. Role-based access control keeps
  collector/annotator identity separate from what each role can see.
- **Photos**: already exist in the photos S3 bucket; an Admin triggers
  `POST /admin/photos/sync` to list bucket objects into the `Photos` table (no
  upload flow needed).
- **Audio**: uploaded and played back via short-lived S3 pre-signed URLs — the API
  never proxies raw audio bytes.
- **SMS OTP**: currently a `ConsoleSmsSender` stub that logs codes to the API
  console (per project decision) — swap in a real provider by implementing
  `ISmsSender` in `backend/src/RecordingApp.Infrastructure/Notifications`.
- `dotnet build` currently reports a moderate-severity advisory (NU1902) against
  the latest available MailKit release; there was no newer patched version at
  build time. Re-check before going to production.
