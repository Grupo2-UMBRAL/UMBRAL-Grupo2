# Refactoring Remediation Plan — 2026-06-18

Post-refactor architecture review (zoom-out + improve-codebase-architecture). Build was
**broken** and Keycloak role authorization was silently disabled. This doc records the
findings and the fix plan.

## Phase 0 — Already fixed (build + tests green: 156/156)

1. **CRITICAL — restored `KeycloakRoleClaimsTransformation`** + re-registered
   `AddSingleton<IClaimsTransformation, …>` in `Umbral.ServiceDefaults`. The class was
   deleted in the "clean shared library" commit, orphaning its test (build error) and
   disabling role mapping. Keycloak emits roles in nested `realm_access` /
   `resource_access` JSON; JWT is configured with flat `RoleClaimType = ClaimTypes.Role`,
   so without the transformation **every `[Authorize(Roles=…)]` / `IsInRole` failed across
   all 4 services.**
2. Renamed 10 stale `ScoringAudit*` files → `ScoringMonitoring*`.
3. Deleted dead port `ILiveSessionStateNotifier` (defined, never used).
4. Fixed session `CONTEXT.md` title `Session Operations` → `Session Management`.

## Phase A — SignalR hub belongs in the Api host, not Application

**Problem:** `Hub<T>` (ASP.NET transport type) is defined in the **Application** layer of
both `scoring-monitoring` and `session-management`. It compiles only because
`Umbral.ServiceDefaults` drags `Microsoft.AspNetCore.App` transitively into every layer.

**Fix:** move the hub and its SignalR publisher/notifier adapter into the **Api** project
(the host that already calls `AddSignalR()` and `MapHub<>`). Keep the strongly-typed client
contract interface (`IScoringMonitoringClient` / `ISessionClient`) + payload records in
Application (no framework dependency). Move their DI registration to `Program.cs`. Drop the
now-redundant explicit `FrameworkReference` from the moved-out projects.

## Phase B — DB schema names still carry old bounded-context names

`session_operations` → `session_management`, `scoring_ops` → `scoring_monitoring` across
migrations + Designer + ModelSnapshot + `*Persistence.SchemaName` (the schema is sourced
from the const via `HasDefaultSchema`, so the model and snapshot stay consistent by
construction — no migration regeneration needed). **Assumption:** dev databases are
recreated from migrations (no provisioned prod data to preserve).

This surfaced **pre-existing inconsistencies** that were also fixed:
- `infra/postgres/init/00-create-schemas.sql` created `mission_design` + `session_operations`
  (stale) — now `mission_management` + `session_management` (+ `scoring_monitoring`).
- `docker-compose.dev.yml` connection-string `Search Path`s for session/scoring were stale.
- `ScoringMonitoring.Api/appsettings.json` `Search Path` was `scoring_ops`.
- Session's `appsettings.json` already said `session_management` while its migrations/const
  said `session_operations` — a latent mismatch (app's search_path wouldn't find its own
  tables); the rename resolves it.

## Phase C — transitive vulnerability

`System.Security.Cryptography.Xml 9.0.0` (NU1903, high severity) appeared **only** in
`MissionManagement.Infrastructure` — the one Infrastructure project without a
`FrameworkReference Microsoft.AspNetCore.App`. The sibling projects don't warn because that
reference enables **framework package pruning**, which keeps the in-box (patched) assembly
out of the restore graph. The standalone NuGet package is flagged at *every* version
(9.0.0 and 10.0.0), so pinning does not help. Fix: give `MissionManagement.Infrastructure`
the same `FrameworkReference` as its siblings (and drop the now-redundant explicit
`Microsoft.Extensions.Configuration.*` package refs). Result: **0 build warnings.**

> Lesson: Phase A initially removed the `FrameworkReference` from
> `ScoringMonitoring.Infrastructure` as "redundant" — that re-introduced the NU1903 warning
> there. These Infrastructure `FrameworkReference`s are load-bearing (pruning); kept.

## Phase D — hygiene

- Rename port `IMissionManagementLiveSessionCatalog` → `IEligibleMissionCatalog` (a port
  should be named for the domain concept it serves, not the upstream service).
- Add explicit `Domain` `ProjectReference` to `UserManagement.Infrastructure` and
  `UserManagement.Api.Tests` (currently resolved only transitively).
- Note the bounded-context renames in `plan.md` / `progress.md` (those docs still say
  `mission-design`).

## Known follow-up (not in this pass)

**Stale env-var names.** `.env` / `.env.example` / `.env.deploy.example` + `docker-compose.dev.yml`
still use old-context variable *names*: `MISSION_DESIGN_PORT`, `SESSION_OPERATIONS_PORT`,
`NEXT_PUBLIC_SESSION_OPERATIONS_HUB_PATH`, `MOBILE_SESSION_OPERATIONS_HUB_PATH`. The *values*
are correct. Renaming spans the web + mobile apps that read them, so it was left out to avoid
breaking the frontends in a backend pass.

**`Umbral.ServiceDefaults` drags ASP.NET Core into the Domain layer.** Every `*.Domain`
references it solely for `UmbralDomainException`, but the assembly also pulls JwtBearer +
`Microsoft.AspNetCore.App`. Recommended: split an `Umbral.ServiceDefaults.Abstractions`
(exceptions + roles, zero framework) and repoint Domain/Application at it. Larger churn —
deferred.
