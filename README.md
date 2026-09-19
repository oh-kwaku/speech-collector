# Speech Collector

Data collection app for capturing children's (ages 5-10) speech: a photo is shown,
the child describes it, a **Collector** records the audio, an **Annotator**
transcribes it, and an **Admin** manages users and exports metadata for the ML
pipeline. Speakers are identified only by a generated `speakerId` + gender + age —
no names or other PII are ever collected.

## Stack

- `backend/` — ASP.NET Core 8 Web API, EF Core (Postgres), JWT auth, S3 pre-signed
  URLs, Gmail SMTP for invite/OTP email.
- `frontend/` — React + TypeScript + Vite + Tailwind, mobile-first collector UI.

## Local development

`docker-compose.yml` at the repo root starts just the backing services
(Postgres + MinIO) with working defaults and no `.env` file required:

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

## Tests

```bash
cd backend
dotnet test
```

## Deploying to production (Dokploy)

`docker-compose.dokploy.yml` is the full app stack (Postgres, MinIO, API,
web) meant to be deployed as a Dokploy "Docker Compose" application, built
directly from this GitHub repo — Dokploy runs `docker compose build` against
the `build:` sections in that file itself, so no image registry is involved.

It intentionally has no Caddy service and publishes no host ports: Dokploy
runs its own Traefik instance and handles HTTPS/domain routing per service
from its dashboard, not from the compose file. Steps:

1. In Dokploy, create a Compose application pointed at this repo, with
   Compose Path set to `docker-compose.dokploy.yml`.
2. Fill in the variables from `.env.example` under the app's Environment tab
   (not a committed `.env` file).
3. Enable auto-deploy on push if wanted; a push rebuilds the whole stack
   (every service with a `build:`), not just whichever of frontend/backend
   changed.
4. After the first deploy, add Domains in Dokploy for:
   - service `web`, container port 80 → your main app domain, path `/`
   - service `api`, container port 8080 → same domain, path `/api` (or a
     dedicated `api.*` subdomain)
   - service `minio`, container port 9000 → a dedicated subdomain (e.g.
     `s3.yourdomain.com`) — this must be reachable by the *browser*, since
     the API hands out pre-signed URLs against `S3_SERVICE_URL` directly to
     it. Leave the MinIO console (port 9001) without a domain; it's not
     meant to be public — reach it via `docker exec`/an SSH tunnel instead.
   (Exact steps depend on your Dokploy version's UI — check its current
   docs.)

To use real AWS S3 or another external provider instead of the bundled
MinIO, repoint the `S3_*` variables and remove the `minio`/`minio-init`
services from `docker-compose.dokploy.yml`.

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
