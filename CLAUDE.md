# Speech Collector — Requirements

> **Maintenance instruction (read this first):** This file is the source of truth
> for what this app must do. Whenever the user states a new requirement, changes
> an existing one, or clarifies scope in conversation, update the relevant section
> below in the same turn — don't wait to be asked separately. Keep entries short
> and dated only when it's a change to something already shipped (so history isn't
> lost). Don't let this file drift from what's actually true; if the code and this
> file disagree, ask the user which is correct rather than guessing.

## What this app is

A data collection tool for an ML project: children ages 5–10 are shown a photo and
describe what they see out loud. The audio is recorded, transcribed/annotated, and
exported as training data.

## Roles

- **Collector** — captures audio recordings of children describing photos.
- **Annotator** — sees all recordings, writes/edits annotations (transcriptions) of
  what was said. Can see all annotations made.
- **Admin** — access to all views. Manages users (invites). Can download all
  recording metadata as Excel/CSV.
- **Multi-role**: a user can hold both Collector and Annotator at once; Admin is
  always exclusive (never combined with another role). Roles are assigned at
  invite time (checkboxes, Admin deselects the others and vice versa) and
  editable later from the Users page. The nav bar shows the union of menu items
  for all of a user's roles, so a Collector+Annotator sees both areas; on
  login/invite-accept they land on their primary field task (Collector first,
  then Annotator, Admin always `/dashboard`), and `/dashboard` itself stacks a
  section per role they hold. Backend: `[Authorize(Roles="A,B")]` already
  matches a user with *any* of several role claims (ASP.NET Core default), so
  per-endpoint authorization needed no changes — only role storage (a
  `UserRoleAssignment` join table, not a single `User.Role` column), JWT claim
  issuance (one `role` claim per assigned role), and the frontend needed
  updating. (2026-09-16)

## Core requirements

- **Auth**: users are invited by email or phone number (OTP-based, no passwords).
  Roles are assigned at invite time. There is no seeded default admin login — since
  inviting requires an existing Admin, the API seeds exactly one Admin on first
  startup (if none exists) from `Bootstrap:AdminEmail`/`AdminPhoneNumber` config
  (`BOOTSTRAP_ADMIN_EMAIL`/`BOOTSTRAP_ADMIN_PHONE` in `.env`). See README "First
  login" section.
- **Speaker capture flow** (Collector):
  1. Tap "New speaker" → inline form (Gender, Age) with Save/Cancel.
  2. On Save: a speaker ID is generated server-side and saved with the form data.
  3. Navigates to a "Speaker {id}" page where recording sessions begin.
- **Recording flow** (Collector, on the Speaker's session page):
  1. Tap "New recording" → opens a page showing a photo for the child to describe.
  2. Controls: **Record, Stop, Play, Retake, Save, Cancel**.
     - Retake overwrites the current (unsaved) take.
     - Cancel exits back to the list of recordings for that speaker session.
     - After Save, a **Next** button appears that loads a new photo to start
       another recording.
  3. A counter showing the number of recordings for that speaker session is shown
     at the top of the recording page.
- **Annotator UI**: a page/queue to annotate recordings, plus a view of all
  annotations already made. Admins also have annotation permission (can use the
  same queue/all-annotations views as Annotators). (2026-09-16)
  - The "all annotations" list lets the user play the recording's audio inline
    (via the shared no-download `AudioPlayer`). Admins additionally see which
    user collected the recording and which user annotated it (email/phone,
    each in its own column); plain Annotators don't see either column.
    (2026-09-16)
  - Every place a recording's audio is listed (annotation queue, all
    annotations, admin recordings browser, collector session view, collector
    history) shows the associated photo — either inline (queue, admin
    browser) or as a "View photo" button that opens the photo in an in-page
    modal (shared `PhotoModal` component; all annotations, collector session
    view, collector history) — so a user can always see what the child was
    describing without leaving the page. (2026-09-16)
- **Home page**: on login/invite-accept, Admins land on `/dashboard`; Collectors
  land on their speakers list; Annotators land on their annotation queue.
  (2026-09-16)
- **Collector history**: Collectors can view all of their own completed
  (confirmed) recordings filtered by a date range, across speakers/sessions, at
  `/collector/history`. (2026-09-16)
- **Dashboards** (`/dashboard`, content varies by role): (2026-09-16)
  - Admin: total recordings, total annotations, recordings broken down by
    speaker gender and by speaker age, plus a per-user work summary table
    (Name, Location, total recordings, total speakers, total annotations —
    deliberately excludes email/phone/role, which live on the Users page
    instead). (2026-09-16)
  - Annotator: total annotations they personally have made.
  - Collector: total speakers they've recorded, total confirmed/unconfirmed
    recordings, all filterable by date range; links to their full history
    page. (2026-09-16)
- **Staff user profiles**: in addition to Email/PhoneNumber (used for OTP
  login), the system captures each staff user's Name and Location (free text,
  e.g. field site/region). Set at invite time or edited later by an Admin on
  the Users page. This is about staff (Collector/Annotator/Admin) accounts,
  not Speakers — the Speaker anonymization rule above is unaffected.
  (2026-09-16)
- **Admin export**: downloadable Excel/CSV with columns exactly:
  `photoId, sessionId, userId, speakerId, annotation, created_at, speaker_gender,
  speaker_age`. Includes every **confirmed** recording, whether or not it has
  been annotated yet — `annotation` is left blank for unannotated rows.
  Unconfirmed (in-progress) recordings are excluded. (2026-09-16)
- **Photos**: already exist in an S3 bucket — users never upload them; the app
  only references/lists what's already there.
- **Audio filenames**: stored objects are named
  `{speakerId}_{audioId}_data_{gender}_{timestamp}.webm` (timestamp = upload-time
  UTC timestamp), under an `audio/{speakerId}/{sessionId}/` prefix. (2026-09-16)
- **Security / anonymization**: data must be highly secured, and speakers must not
  be identifiable in any way.
  - Decision: **metadata-level anonymization** — no name/DOB/contact info is ever
    collected for a speaker, only a generated `speakerId` + gender + age. Strict
    role-based access control. Raw audio itself is kept unmodified (it's ML
    training data), i.e. no voice-disguising/pitch-shifting.
  - Audio playback UI must not offer a download affordance on any page (native
    player "download" control and right-click "save audio as" are both
    disabled) — shared `AudioPlayer` component, used everywhere audio is
    played. This only removes browser UI shortcuts, not a technical
    guarantee against extraction — that's what short-lived pre-signed URLs +
    RBAC are for. (2026-09-16)
- **Mobile-friendly**: the data-collector-facing pages must work well on mobile
  (this is the primary device collectors use in the field).

## Tech stack & key decisions

- **Frontend**: React + TypeScript + Vite + Tailwind CSS, `react-router-dom`.
- **Backend**: C# / ASP.NET Core 8 Web API, EF Core + Npgsql (Postgres), JWT auth.
- **Data storage**: Postgres on a VPS; audio + photos in object storage via a
  generic S3-compatible connection (`IAmazonS3` configured through app settings —
  works against AWS S3 or any S3-compatible provider such as MinIO/DigitalOcean
  Spaces/Cloudflare R2/Backblaze B2 by setting `S3:ServiceUrl` +
  `S3:ForcePathStyle`, not hardcoded to AWS). Pre-signed URLs only, no public
  buckets, no proxying raw bytes through the API. (2026-09-16)
- **Invite/OTP delivery**: email via Gmail SMTP; SMS behind an `ISmsSender`
  interface with a `ConsoleSmsSender` stub (logs codes to the console) — swap in a
  real provider (Twilio/SNS/etc.) later without touching callers.
- **Frontend `VITE_API_BASE_URL` is read at container startup, not baked in
  at build time**: a `docker-entrypoint.d/env-config.sh` script in the nginx
  image regenerates `env-config.js` (loaded by `index.html` before the app
  bundle, read in `frontend/src/api/client.ts` via `window.__RUNTIME_CONFIG__`)
  from the container's actual env every time it starts. This was originally
  a Vite build-time-only `ARG`/`ENV` in `frontend/Dockerfile`, which meant an
  env var set in Dokploy's Environment tab was silently ignored (nginx never
  reads env vars, and the value was already permanently baked into the JS
  bundle at `npm run build`) — changing it needed a Dokploy *Build Arg* and a
  full rebuild. Now a plain Environment Variable + restart is enough on
  either Dokploy topology below; the build-time `ARG` still sets the
  fallback default (`/api`) used by `vite build`/`vite preview` without
  Docker, and by the GitHub Pages build below, which has no running
  container to read env vars from at all. (2026-09-20)
- **Deployment**: production runs on a VPS via **Dokploy**. `web` and `api`
  are each deployed as their own Dokploy "Application" (Dockerfile)
  service — `web` from `frontend/Dockerfile`, `api` from `backend/Dockerfile`
  — rather than as one Docker Compose stack. Postgres runs on Dokploy's
  built-in Database feature; MinIO (the default S3-compatible object store)
  is its own standalone Dokploy app. No Caddy: Dokploy's own Traefik handles
  HTTPS/domain routing per service via its dashboard. (2026-09-20)
  - `docker-compose.dokploy.yml` bundles the whole stack (Postgres, MinIO +
    `minio-init`, API, web) as a single Dokploy "Docker Compose" application
    instead — kept as an alternative/reference deployment path, not the one
    currently in use. Its bundled MinIO needs its own Dokploy-configured
    subdomain (routed to container port 9000) since the API hands out
    pre-signed URLs the browser must resolve directly; the MinIO admin
    console is intentionally not exposed via a domain (reach it via SSH port
    forwarding). (2026-09-16, revised 2026-09-20)
  - `docker-compose.yml` (plain, no Dokploy) is separate and only for local
    dev. Default (`docker compose up -d`, no `.env` needed) starts just
    Postgres + MinIO backing services; the API/frontend run natively against
    it, per the README. (2026-09-17) A `full` profile (`docker compose
    --profile full up -d --build`) additionally containerizes the API/web
    themselves (via the same Dockerfiles used for production) for a
    one-command local deployment when you just want the app running rather
    than to edit it — seeds a dev-only phone-OTP Admin so it's immediately
    usable, and uses host networking on the `api` service (Linux-only) since
    the app's S3 pre-signed URLs need the same hostname to resolve for both
    the container and the browser. (2026-09-19)
- **GitHub Pages**: the frontend alone can also be published as a static
  site (`frontend/`'s `npm run deploy`, via the `gh-pages` package pushing
  `dist/` to a `gh-pages` branch) — independent of the Dokploy deployment
  above, for a demo/staging frontend against a backend deployed elsewhere.
  Since there's no container at all here, `VITE_API_BASE_URL` must be passed
  at build time (`VITE_API_BASE_URL=https://... npm run deploy`, run
  locally); the target API's CORS config (`Cors:AllowedOrigins`) must allow
  the `https://<owner>.github.io` origin. `vite.config.ts`'s `base:
  '/speech-collector/'` must match the repo name (GitHub Pages project sites
  are served from `/<repo>/`). Client-side routes survive refresh/deep-link
  despite GitHub Pages having no rewrite rules, via a redirect trick in
  `frontend/public/404.html` + `frontend/index.html`. See README for full
  steps. (2026-09-20)
  - `.github/workflows/deploy-gh-pages.yml` automates the above: runs on
    push to the `frontend`/`master` branches touching `frontend/**` or via
    manual `workflow_dispatch`, builds with `VITE_API_BASE_URL` sourced from
    a GitHub Actions variable — not a secret, since it ends up in public
    client-side JS regardless — and pushes `frontend/dist` to `gh-pages` via
    `peaceiris/actions-gh-pages` using the built-in `GITHUB_TOKEN`
    (`permissions: contents: write`, no extra secrets to configure). The
    manual `npm run deploy` path still works as a fallback but pushes to the
    same branch, so don't run both for one release. The job declares
    `environment: github-pages` so it can read `vars.VITE_API_BASE_URL` set
    under Settings → Environments → `github-pages` → Environment variables
    — a *repository*-level variable (Settings → Secrets and variables →
    Actions → Variables tab) needs no `environment:` key instead; a job
    without a matching `environment:` key silently sees neither the
    Environment's variables nor its secrets. (2026-09-20)
  - GitHub Pages serves the app at `/speech-collector/`, so
    `BrowserRouter` in `frontend/src/main.tsx` needs
    `basename={import.meta.env.BASE_URL}` — without it, React Router
    resolves every `<Navigate>`/redirect against the domain root (e.g.
    `/login` instead of `/speech-collector/login`), 404ing after auth
    redirects. The one raw `window.location.assign(...)` outside router
    context (401 handler in `frontend/src/api/client.ts`) needs the same
    `import.meta.env.BASE_URL` prefix manually, since `basename` doesn't
    apply to it. (2026-09-20)
  - `frontend/vite.config.ts`'s `base` must NOT be an unconditional
    `/speech-collector/` — that also bakes the prefix into the **Docker**
    build's `dist/index.html` asset paths, but `nginx.conf` there serves
    from `/` and only maps `/assets/`, so every JS/CSS request 404s and
    nginx's SPA fallback (`try_files $uri /index.html`) serves back
    `index.html` in their place — a blank page, no console error beyond a
    script-parse failure. Fixed by making `base` conditional:
    `process.env.GH_PAGES === 'true' ? '/speech-collector/' : '/'`, with
    `GH_PAGES=true` set only by `frontend/package.json`'s `predeploy`
    script and by `deploy-gh-pages.yml`'s build step — the plain
    `npm run build` used by `frontend/Dockerfile` leaves it unset, so
    Docker/Dokploy builds still get `base: '/'`. (2026-09-20)

## Where things live

- `backend/` — ASP.NET Core API (`src/RecordingApp.Api`, `.Domain`,
  `.Infrastructure`; tests in `tests/RecordingApp.Tests`).
- `frontend/` — React app (`src/pages/{collector,annotator,admin}`, `src/api`,
  `src/auth`).
- `docker-compose.yml` (local dev backing services), `docker-compose.dokploy.yml`
  (production, deployed via Dokploy), `.env.example` (vars for the Dokploy
  stack) — deployment config at the repo root.
- Full implementation plan/architecture notes: see the plan this was built from
  (DB schema, API endpoints, S3 layout) — re-derive from the code if it's not
  otherwise available, since this file tracks *requirements*, not implementation
  detail.

## Status

Initial implementation is built and smoke-tested end-to-end (auth/invite flow,
speaker → session → recording capture, annotation, CSV/XLSX export all verified
against a real Postgres instance). Not yet deployed to a real VPS; S3 (or
S3-compatible) and Gmail credentials still need to be filled in before photo
sync / audio upload-playback / real email delivery will work.
