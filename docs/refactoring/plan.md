# 🏗️ Backend Refactoring Plan — Final (v2)

## Decisions Locked In

| Question | Decision |
|----------|----------|
| Target Framework | `net10.0` for everything |
| Shared Library name | Keep `Umbral.ServiceDefaults` — it IS a real shared class library |
| `CurrentUser` abstraction | **Delete it.** Replace with direct `ClaimsPrincipal` usage. Rewrite `RequestAuthorizationBehavior` to use `IHttpContextAccessor` + `ClaimsPrincipal` directly |
| Bootstrap logic | **Move out of Application layer.** Migrations belong in Infrastructure or directly in `Program.cs`. Keep bootstrap tests but rewrite them against the new location |
| Test strategy | **Unit tests for handlers.** Test handlers directly with injected dependencies (real or faked DbContext, no MediatR mocking). Keep existing integration tests too |
| Solution structure | **One root `.sln`** with solution folders per bounded context. Kill the individual `.slnx` files |

---

## Target Architecture

```
src/
├── shared/
│   └── Umbral.ServiceDefaults/               (Class Library — net10.0)
│       ├── AuthConfiguration.cs
│       ├── BaseController.cs
│       ├── ServiceConfiguration.cs
│       ├── UmbralExceptionHandler.cs
│       ├── UmbralServiceException.cs
│       ├── UmbralDomainException.cs
│       ├── UmbralTechnicalException.cs
│       ├── UmbralFailureCategory.cs
│       └── UmbralRoles.cs                    (merged with AuthorizationPolicies)
│
├── services/
│   ├── mission-design/
│   │   ├── MissionDesign.Domain/             (Class Library)
│   │   │   └── Missions/                    (Entities, Value Objects, Enums)
│   │   ├── MissionDesign.Application/        (Class Library)
│   │   │   ├── Abstractions/                (IMissionDesignDbContext)
│   │   │   └── Features/
│   │   │       ├── Missions/
│   │   │       │   ├── Commands/
│   │   │       │   │   └── CreateMission/
│   │   │       │   │       ├── CreateMissionCommand.cs
│   │   │       │   │       └── CreateMissionCommandHandler.cs
│   │   │       │   └── Queries/
│   │   │       │       └── ListMissions/
│   │   │       └── MissionStages/
│   │   │           ├── Commands/
│   │   │           └── Queries/
│   │   ├── MissionDesign.Infrastructure/     (Class Library)
│   │   │   ├── Persistence/
│   │   │   │   ├── MissionDesignDbContext.cs
│   │   │   │   ├── Configurations/          (Fluent API)
│   │   │   │   └── Migrations/
│   │   │   └── ServiceCollectionExtensions.cs
│   │   └── MissionDesign.Api/                (Web API)
│   │       ├── Controllers/
│   │       └── Program.cs
│   │
│   ├── scoring-audit/                        (same 4-project pattern)
│   ├── session-operations/                   (same 4-project pattern)
│   └── user-management/                      (already split — just cleanup)
│
└── tests/
    ├── MissionDesign.Api.Tests/
    ├── ScoringAudit.Api.Tests/
    ├── SessionOperations.Api.Tests/
    └── UserManagement.Api.Tests/

Umbral.sln                                    (root — one solution for everything)
```

---

## Phase 1: Clean Shared Library

> ~15 files affected. Estimated: 1-2 hours.

### Delete (YAGNI)
- [ServiceIdentity.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/ServiceIdentity.cs) — constructed in every Program.cs, never consumed
- [IServiceBootstrapDetailsProvider.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/IServiceBootstrapDetailsProvider.cs) — one implementation per service, ceremony only
- [ServiceBootstrapDetails.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/ServiceBootstrapDetails.cs) — carries trivially-available config
- [IServicePersistenceInitializer.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/IServicePersistenceInitializer.cs) — wraps a single `MigrateAsync()` call

### Delete (Replace with framework)
- [CurrentUser.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/CurrentUser.cs) — replaced by `ClaimsPrincipal`
- [ICurrentUserAccessor.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/ICurrentUserAccessor.cs) — replaced by `IHttpContextAccessor`
- [HttpContextCurrentUserAccessor.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/HttpContextCurrentUserAccessor.cs) — framework already provides this

### Merge
- Merge [UmbralAuthorizationPolicies.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/UmbralAuthorizationPolicies.cs) into [UmbralRoles.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/UmbralRoles.cs) — same string constants duplicated

### Modify
- [ServiceCollectionExtensions.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/ServiceCollectionExtensions.cs):
  - Remove `ICurrentUserAccessor` registration
  - Use `UmbralRoles` constants directly in authorization policies
  - Remove MediatR dependency from shared (MediatR should not be in the shared library)
- [Umbral.ServiceDefaults.csproj](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/Umbral.ServiceDefaults.csproj):
  - Change TFM from `net8.0` to `net10.0`
  - Remove `MediatR` package reference (doesn't belong in shared)

### Rewrite
- Update all shared test files to remove references to deleted types
- Rewrite `RequestAuthorizationBehaviorTests` to use `ClaimsPrincipal` directly instead of `CurrentUser`

> [!WARNING]  
> The `RequestAuthorizationBehavior` in `session-operations` uses `ICurrentUserAccessor` and `CurrentUser`. This pipeline will be rewritten to use `IHttpContextAccessor` → `HttpContext.User` (a `ClaimsPrincipal`) directly, with role checks via `ClaimsPrincipal.IsInRole()`.

---

## Phase 2: Split `mission-design` into 4 Projects

> ~30 files moved/renamed. Estimated: 2-3 hours.

### [NEW] `MissionDesign.Domain` (Class Library)
- Move from `Umbral.MissionDesign.Api/Domain/Missions/`:
  - `Mission.cs`, `MissionGameType.cs`, `MissionHint.cs`, `MissionNode.cs`, `MissionStage.cs`, `MissionStageDifficulty.cs`, `MissionStageHint.cs`
- Namespace: `MissionDesign.Domain.Missions`
- Dependencies: **None** (pure domain — not even `Umbral.ServiceDefaults`)

> [!IMPORTANT]
> Domain entities currently reference `UmbralDomainException` from the shared library. This is the ONE acceptable shared dependency for Domain, since the exception hierarchy is a genuine cross-cutting concern. The Domain project will reference `Umbral.ServiceDefaults` only for this.

### [NEW] `MissionDesign.Application` (Class Library)
- Move from `Umbral.MissionDesign.Api/Application/`:
  - `IMissionDesignDbContext.cs` → `Abstractions/`
  - All feature files → split into separate Command/Handler/Validator files under `Features/`
  - `MissionContracts.cs`, `MissionStageContracts.cs` → `Features/` (DTOs/Responses)
- Delete: `Application/Bootstrap/` folder entirely
- Dependencies: `MissionDesign.Domain`, `Umbral.ServiceDefaults`, `MediatR`

### [NEW] `MissionDesign.Infrastructure` (Class Library)
- Move from `Umbral.MissionDesign.Api/Infrastructure/`:
  - `MissionDesignDbContext.cs` → `Persistence/`
  - All migrations → `Persistence/Migrations/`
  - `MissionDesignPersistence.cs`, `MissionDesignPersistenceInitializer.cs` → `Persistence/`
  - `ServiceCollectionExtensions.cs`
- Delete: `MissionDesignBootstrapDetailsProvider.cs` (YAGNI)
- Dependencies: `MissionDesign.Application`, `MissionDesign.Domain`, `Umbral.ServiceDefaults`, EF Core, Npgsql

### [MODIFY] `MissionDesign.Api` (Web API)
- Keep: `Program.cs`, `Controllers/` (from current `Presentation/`), `GlobalUsings.cs`
- Modify `Program.cs`:
  - Remove `ServiceIdentity` construction
  - Remove `Bootstrap` command usage
  - Inline migration: `await dbContext.Database.MigrateAsync()`
  - Drop all `Umbral.MissionDesign.Api.*` using statements → replace with new namespaces
- Dependencies: `MissionDesign.Application`, `MissionDesign.Infrastructure`, `Umbral.ServiceDefaults`

### [DELETE] `MissionDesign.slnx` (if any) — replaced by root `Umbral.sln`

---

## Phase 3: Split `scoring-audit` into 4 Projects

> Same pattern as Phase 2. ~25 files moved/renamed.

Same decomposition. Additional considerations:
- Has SignalR hub (`ScoringAuditHub.cs`) — stays in the Api project under `Hubs/`
- Has `Presentation/Realtime/SignalRScoringAuditUpdatesPublisher.cs` — this is Infrastructure, move to `ScoringAudit.Infrastructure`
- Split all combined CQRS files in `Application/`

---

## Phase 4: Split `session-operations` into 4 Projects

> Largest service. ~50+ files moved/renamed. Estimated: 3-4 hours.

Same decomposition. Additional considerations:
- Has SignalR hub — stays in Api
- Has `ISessionRealtimeNotifier` abstraction — stays in Application (it's a port)
- `SignalRLiveSessionStateNotifier` — moves to Infrastructure (it's an adapter)
- `HttpContextCurrentOperatorIdentity` / `HttpContextCurrentParticipantIdentity` — moves to Infrastructure
- `AuthHeaderForwardingHandler`, `ScoringAuditHttpClient`, `MissionDesignLiveSessionCatalog` — all Infrastructure
- **Rewrite `RequestAuthorizationBehavior`** to use `ClaimsPrincipal` directly instead of `CurrentUser`

---

## Phase 5: Clean `user-management`

> Small cleanup. ~5 files affected.

### Delete
- [WeatherForecastController.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/services/user-management/UserManagement.Api/Controllers/WeatherForecastController.cs) — template junk
- [WeatherForecast.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/services/user-management/UserManagement.Api/WeatherForecast.cs) — template junk
- [UnitTest1.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/services/user-management/UserManagement.Api.Tests/UnitTest1.cs) — empty template
- [UserManagement.slnx](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/services/user-management/UserManagement.slnx) — replaced by root `Umbral.sln`

### Modify
- All `.csproj` files — already on `net10.0`, verify no stale references
- `Program.cs` — remove `ServiceIdentity` construction

---

## Phase 6: Create Root Solution + Align Packages

### [NEW] `Umbral.sln` at repository root
- Solution folders mirroring the directory structure
- All 17+ projects included with proper grouping

### Package Alignment
- Standardize all projects to `net10.0`
- Upgrade MediatR to v12+ (unified package) across all services
- Upgrade EF Core from `8.0.8` to `10.x`
- Upgrade `Microsoft.AspNetCore.Authentication.JwtBearer` to `10.x`
- Upgrade test packages (`xunit`, `Microsoft.NET.Test.Sdk`, etc.) to latest
- Consider Central Package Management (`Directory.Packages.props`) to prevent version drift

---

## Verification Plan

### After Each Phase
```powershell
dotnet build Umbral.sln
dotnet test Umbral.sln
```

### After All Phases
```powershell
# Full local validation
.\scripts\Invoke-RepositoryValidation.ps1

# Docker smoke test
docker-compose -f docker-compose.dev.yml up --build
```

### Unit Test Coverage Goals
- Every handler gets at least 2 unit tests (happy path + primary error case)
- Handlers tested by instantiating them directly with a faked `DbContext` (EF InMemory) — **no MediatR mocking**
- Controller tests via `WebApplicationFactory` for integration coverage

---

## Scope Summary

| Metric | Count |
|--------|-------|
| Projects created | 9 new `.csproj` (3 services × 3 new layers each) |
| Projects modified | 8 existing projects (4 Api + 4 test) |
| Files deleted | ~25 (YAGNI, template junk, duplicate solutions) |
| Files moved/renamed | ~100 (namespace changes across 3 services) |
| Files rewritten | ~5 (authorization pipeline, Program.cs files) |
| New solution | 1 root `Umbral.sln` replacing 2+ `.slnx` files |
