# UMBRAL — Project Guide for Claude

UMBRAL: real-time immersive mission game (Treasure Hunt + Trivia). .NET microservices
backend + React web (operator/admin console) + Expo mobile (participants), behind a YARP
edge-proxy. Keycloak auth, Postgres, RabbitMQ, SignalR realtime.

## Where things live
- `src/services/{user,mission,session,scoring-monitoring}-management` — 4 bounded contexts,
  each split `.Api` / `.Application` / `.Domain` / `.Infrastructure` + `tests/` + a
  `CONTEXT.md` (**domain language lives in those CONTEXT.md files**).
- `src/apps/web` — React 19 + Vite + TS. **Operator** dashboard = live sessions;
  **Admin** dashboard = mission authoring + operator users. Single-file design system:
  `src/apps/web/src/index.css`.
- `src/apps/mobile` — Expo / RN 0.81, `expo-router`. Palette tokens: `src/apps/mobile/src/theme/tokens.ts`.
- `src/apps/edge-proxy` — `Umbral.EdgeProxy`, YARP reverse proxy / API gateway.
- `src/shared/Umbral.ServiceDefaults` — auth/JWT + Keycloak role claims, MediatR logging +
  FluentValidation pipeline behaviors, exception hierarchy.

## Read before changing things
- Architecture: `docs/architecture/repo-structure.md`, ADRs in `docs/architecture/adr/`.
- **Endpoints the web consumes**: `docs/architecture/frontend-endpoints-summary.md`.
- **Who can do what** (Admin / Operator / Participant): `docs/architecture/authorization-matrix.md`.
- Design direction: `docs/product/design.md` (web = calm/operational; mobile = Duolingo-bright).
  The web palette is being aligned to the **mobile** palette (`src/apps/mobile/src/theme/tokens.ts`).
- Spec + vocabulary: `docs/product/ers-umbral.md`, `docs/product/glossary.md`.
  Front vocabulary: **etapa / misión / sesión** — never "sección" / "play".
- Contribution rules: `AGENTS.md` (hard rules, TDD, no-ff merges).
- In-flight web refactor follow-ups: `docs/web-refactor-followups.md`.

## Stack quick ref
.NET 10 · EF Core 10 + Npgsql → Postgres 16 · MediatR 12 + FluentValidation 11 · Keycloak 25 ·
RabbitMQ 3.13 · SignalR · Web: React 19 / Vite / TS.

## Run locally
`docker compose -f docker-compose.dev.yml up -d` (or `scripts/Start-Dev.ps1`).
Web `:3000`, mobile `:19006`, edge-proxy `:7500`. DB is disposable (pre-release) — no prod data.
