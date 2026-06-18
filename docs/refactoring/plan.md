# ðŸ—ï¸ Backend Refactoring Plan â€” Final (v2)

## Decisions Locked In

| Question | Decision |
|----------|----------|
| Target Framework | `net10.0` for everything |
| Shared Library name | Keep `Umbral.ServiceDefaults` â€” it IS a real shared class library |
| `CurrentUser` abstraction | **Delete it.** Replace with direct `ClaimsPrincipal` usage. Rewrite `RequestAuthorizationBehavior` to use `IHttpContextAccessor` + `ClaimsPrincipal` directly |
| Bootstrap logic | **Move out of Application layer.** Migrations belong in Infrastructure or directly in `Program.cs`. Keep bootstrap tests but rewrite them against the new location |
| Test strategy | **Unit tests for handlers.** Test handlers directly with injected dependencies (real or faked DbContext, no MediatR mocking). Keep existing integration tests too |
| Solution structure | **One root `.sln`** with solution folders per bounded context. Kill the individual `.slnx` files |

---

## Target Architecture

```
src/
â”œâ”€â”€ shared/
â”‚   â””â”€â”€ Umbral.ServiceDefaults/               (Class Library â€” net10.0)
â”‚       â”œâ”€â”€ AuthConfiguration.cs
â”‚       â”œâ”€â”€ BaseController.cs
â”‚       â”œâ”€â”€ ServiceConfiguration.cs
â”‚       â”œâ”€â”€ UmbralExceptionHandler.cs
â”‚       â”œâ”€â”€ UmbralServiceException.cs
â”‚       â”œâ”€â”€ UmbralDomainException.cs
â”‚       â”œâ”€â”€ UmbralTechnicalException.cs
â”‚       â”œâ”€â”€ UmbralFailureCategory.cs
â”‚       â””â”€â”€ UmbralRoles.cs                    (merged with AuthorizationPolicies)
â”‚
â”œâ”€â”€ services/
â”‚   â”œâ”€â”€ mission-design/
â”‚   â”‚   â”œâ”€â”€ MissionDesign.Domain/             (Class Library)
â”‚   â”‚   â”‚   â””â”€â”€ Missions/                    (Entities, Value Objects, Enums)
â”‚   â”‚   â”œâ”€â”€ MissionDesign.Application/        (Class Library)
â”‚   â”‚   â”‚   â”œâ”€â”€ Abstractions/                (IMissionDesignDbContext)
â”‚   â”‚   â”‚   â””â”€â”€ Features/
â”‚   â”‚   â”‚       â”œâ”€â”€ Missions/
â”‚   â”‚   â”‚       â”‚   â”œâ”€â”€ Commands/
â”‚   â”‚   â”‚       â”‚   â”‚   â””â”€â”€ CreateMission/
â”‚   â”‚   â”‚       â”‚   â”‚       â”œâ”€â”€ CreateMissionCommand.cs
â”‚   â”‚   â”‚       â”‚   â”‚       â””â”€â”€ CreateMissionCommandHandler.cs
â”‚   â”‚   â”‚       â”‚   â””â”€â”€ Queries/
â”‚   â”‚   â”‚       â”‚       â””â”€â”€ ListMissions/
â”‚   â”‚   â”‚       â””â”€â”€ MissionStages/
â”‚   â”‚   â”‚           â”œâ”€â”€ Commands/
â”‚   â”‚   â”‚           â””â”€â”€ Queries/
â”‚   â”‚   â”œâ”€â”€ MissionDesign.Infrastructure/     (Class Library)
â”‚   â”‚   â”‚   â”œâ”€â”€ Persistence/
â”‚   â”‚   â”‚   â”‚   â”œâ”€â”€ MissionDesignDbContext.cs
â”‚   â”‚   â”‚   â”‚   â”œâ”€â”€ Configurations/          (Fluent API)
â”‚   â”‚   â”‚   â”‚   â””â”€â”€ Migrations/
â”‚   â”‚   â”‚   â””â”€â”€ ServiceCollectionExtensions.cs
â”‚   â”‚   â””â”€â”€ MissionDesign.Api/                (Web API)
â”‚   â”‚       â”œâ”€â”€ Controllers/
â”‚   â”‚       â””â”€â”€ Program.cs
â”‚   â”‚
â”‚   â”œâ”€â”€ scoring-monitoring/                        (same 4-project pattern)
â”‚   â”œâ”€â”€ session-operations/                   (same 4-project pattern)
â”‚   â””â”€â”€ user-management/                      (already split â€” just cleanup)
â”‚
â””â”€â”€ tests/
    â”œâ”€â”€ MissionDesign.Api.Tests/
    â”œâ”€â”€ ScoringMonitoring.Api.Tests/
    â”œâ”€â”€ SessionOperations.Api.Tests/
    â””â”€â”€ UserManagement.Api.Tests/

Umbral.sln                                    (root â€” one solution for everything)
```

---

## Phase 1: Clean Shared Library

> ~15 files affected. Estimated: 1-2 hours.

### Delete (YAGNI)
- [ServiceIdentity.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/ServiceIdentity.cs) â€” constructed in every Program.cs, never consumed
- [IServiceBootstrapDetailsProvider.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/IServiceBootstrapDetailsProvider.cs) â€” one implementation per service, ceremony only
- [ServiceBootstrapDetails.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/ServiceBootstrapDetails.cs) â€” carries trivially-available config
- [IServicePersistenceInitializer.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/IServicePersistenceInitializer.cs) â€” wraps a single `MigrateAsync()` call

### Delete (Replace with framework)
- [CurrentUser.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/CurrentUser.cs) â€” replaced by `ClaimsPrincipal`
- [ICurrentUserAccessor.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/ICurrentUserAccessor.cs) â€” replaced by `IHttpContextAccessor`
- [HttpContextCurrentUserAccessor.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/HttpContextCurrentUserAccessor.cs) â€” framework already provides this

### Merge
- Merge [UmbralAuthorizationPolicies.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/UmbralAuthorizationPolicies.cs) into [UmbralRoles.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/shared/Umbral.ServiceDefaults/UmbralRoles.cs) â€” same string constants duplicated

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
> The `RequestAuthorizationBehavior` in `session-operations` uses `ICurrentUserAccessor` and `CurrentUser`. This pipeline will be rewritten to use `IHttpContextAccessor` â†’ `HttpContext.User` (a `ClaimsPrincipal`) directly, with role checks via `ClaimsPrincipal.IsInRole()`.

---

## Phase 2: Split `mission-design` into 4 Projects

> ~30 files moved/renamed. Estimated: 2-3 hours.

### [NEW] `MissionDesign.Domain` (Class Library)
- Move from `Umbral.MissionDesign.Api/Domain/Missions/`:
  - `Mission.cs`, `MissionGameType.cs`, `MissionHint.cs`, `MissionNode.cs`, `MissionStage.cs`, `MissionStageDifficulty.cs`, `MissionStageHint.cs`
- Namespace: `MissionDesign.Domain.Missions`
- Dependencies: **None** (pure domain â€” not even `Umbral.ServiceDefaults`)

> [!IMPORTANT]
> Domain entities currently reference `UmbralDomainException` from the shared library. This is the ONE acceptable shared dependency for Domain, since the exception hierarchy is a genuine cross-cutting concern. The Domain project will reference `Umbral.ServiceDefaults` only for this.

### [NEW] `MissionDesign.Application` (Class Library)
- Move from `Umbral.MissionDesign.Api/Application/`:
  - `IMissionDesignDbContext.cs` â†’ `Abstractions/`
  - All feature files â†’ split into separate Command/Handler/Validator files under `Features/`
  - `MissionContracts.cs`, `MissionStageContracts.cs` â†’ `Features/` (DTOs/Responses)
- Delete: `Application/Bootstrap/` folder entirely
- Dependencies: `MissionDesign.Domain`, `Umbral.ServiceDefaults`, `MediatR`

### [NEW] `MissionDesign.Infrastructure` (Class Library)
- Move from `Umbral.MissionDesign.Api/Infrastructure/`:
  - `MissionDesignDbContext.cs` â†’ `Persistence/`
  - All migrations â†’ `Persistence/Migrations/`
  - `MissionDesignPersistence.cs`, `MissionDesignPersistenceInitializer.cs` â†’ `Persistence/`
  - `ServiceCollectionExtensions.cs`
- Delete: `MissionDesignBootstrapDetailsProvider.cs` (YAGNI)
- Dependencies: `MissionDesign.Application`, `MissionDesign.Domain`, `Umbral.ServiceDefaults`, EF Core, Npgsql

### [MODIFY] `MissionDesign.Api` (Web API)
- Keep: `Program.cs`, `Controllers/` (from current `Presentation/`), `GlobalUsings.cs`
- Modify `Program.cs`:
  - Remove `ServiceIdentity` construction
  - Remove `Bootstrap` command usage
  - Inline migration: `await dbContext.Database.MigrateAsync()`
  - Drop all `Umbral.MissionDesign.Api.*` using statements â†’ replace with new namespaces
- Dependencies: `MissionDesign.Application`, `MissionDesign.Infrastructure`, `Umbral.ServiceDefaults`

### [DELETE] `MissionDesign.slnx` (if any) â€” replaced by root `Umbral.sln`

---

## Phase 3: Split `scoring-monitoring` into 4 Projects

> Same pattern as Phase 2. ~25 files moved/renamed.

Same decomposition. Additional considerations:
- Has SignalR hub (`ScoringMonitoringHub.cs`) â€” stays in the Api project under `Hubs/`
- Has `Presentation/Realtime/SignalRScoringMonitoringUpdatesPublisher.cs` â€” this is Infrastructure, move to `ScoringMonitoring.Infrastructure`
- Split all combined CQRS files in `Application/`

---

## Phase 4: Split `session-operations` into 4 Projects

> Largest service. ~50+ files moved/renamed. Estimated: 3-4 hours.

Same decomposition. Additional considerations:
- Has SignalR hub â€” stays in Api
- Has `ISessionRealtimeNotifier` abstraction â€” stays in Application (it's a port)
- `SignalRLiveSessionStateNotifier` â€” moves to Infrastructure (it's an adapter)
- `HttpContextCurrentOperatorIdentity` / `HttpContextCurrentParticipantIdentity` â€” moves to Infrastructure
- `AuthHeaderForwardingHandler`, `ScoringMonitoringHttpClient`, `MissionDesignLiveSessionCatalog` â€” all Infrastructure
- **Rewrite `RequestAuthorizationBehavior`** to use `ClaimsPrincipal` directly instead of `CurrentUser`

---

## Phase 5: Clean `user-management`

> Small cleanup. ~5 files affected.

### Delete
- [WeatherForecastController.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/services/user-management/UserManagement.Api/Controllers/WeatherForecastController.cs) â€” template junk
- [WeatherForecast.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/services/user-management/UserManagement.Api/WeatherForecast.cs) â€” template junk
- [UnitTest1.cs](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/services/user-management/UserManagement.Api.Tests/UnitTest1.cs) â€” empty template
- [UserManagement.slnx](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/services/user-management/UserManagement.slnx) â€” replaced by root `Umbral.sln`

### Modify
- All `.csproj` files â€” already on `net10.0`, verify no stale references
- `Program.cs` â€” remove `ServiceIdentity` construction

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
- Handlers tested by instantiating them directly with a faked `DbContext` (EF InMemory) â€” **no MediatR mocking**
- Controller tests via `WebApplicationFactory` for integration coverage

---

## Scope Summary

| Metric | Count |
|--------|-------|
| Projects created | 9 new `.csproj` (3 services Ã— 3 new layers each) |
| Projects modified | 8 existing projects (4 Api + 4 test) |
| Files deleted | ~25 (YAGNI, template junk, duplicate solutions) |
| Files moved/renamed | ~100 (namespace changes across 3 services) |
| Files rewritten | ~5 (authorization pipeline, Program.cs files) |
| New solution | 1 root `Umbral.sln` replacing 2+ `.slnx` files |
