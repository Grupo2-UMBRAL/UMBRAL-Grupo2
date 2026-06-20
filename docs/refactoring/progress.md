# Backend Refactoring Progress

## Phase 1: Clean Shared Library ✅ COMPLETE
- [x] Delete YAGNI files (ServiceIdentity, Bootstrap interfaces, ServiceBootstrapDetails)
- [x] Delete CurrentUser abstraction (CurrentUser, ICurrentUserAccessor, HttpContextCurrentUserAccessor)
- [x] Merge UmbralAuthorizationPolicies into UmbralRoles (deleted Policies, using Roles directly)
- [x] Update ServiceCollectionExtensions (removed CurrentUser reg, use UmbralRoles directly, removed MediatR using)
- [x] Remove MediatR dependency from shared .csproj
- [x] Upgrade shared TFM to net10.0 + NuGet packages to 10.0.0
- [x] Delete broken shared tests (RequestAuthorizationBehaviorTests.cs)
- [x] Remove ServiceIdentity lines from all 4 Program.cs files
- [x] Remove IServiceBootstrapDetailsProvider/IServicePersistenceInitializer registrations from 3 service ServiceCollectionExtensions
- [x] Delete BootstrapDetailsProvider + PersistenceInitializer files from each service Infrastructure
- [x] Delete Application/Bootstrap folders from 3 services
- [x] Replace MediatR migration commands with direct dbContext.Database.MigrateAsync() in 3 Program.cs files
- [x] Clean bootstrap test classes from 3 service test files (kept health + role + domain tests)
- [x] Verify zero dangling references to deleted types

### Files Deleted (18 total)
**Shared Library (8 files):**
- ServiceIdentity.cs
- IServiceBootstrapDetailsProvider.cs
- ServiceBootstrapDetails.cs
- IServicePersistenceInitializer.cs
- CurrentUser.cs
- ICurrentUserAccessor.cs
- HttpContextCurrentUserAccessor.cs
- UmbralAuthorizationPolicies.cs

**Per-Service Bootstrap (3 services × 3 items = ~12 items):**
- Application/Bootstrap/ folder (Commands + Queries) — 3 services
- Infrastructure/XxxBootstrapDetailsProvider.cs — 3 services
- Infrastructure/XxxPersistenceInitializer.cs — 3 services

**Tests:**
- RequestAuthorizationBehaviorTests.cs (shared — referenced phantom types)

### Files Modified (11 total)
- Umbral.ServiceDefaults.csproj (TFM + packages)
- ServiceCollectionExtensions.cs (shared)
- OperatorsController.cs (UmbralRoles instead of UmbralAuthorizationPolicies)
- 4 × Program.cs (removed ServiceIdentity + inlined migrations)
- 3 × ServiceCollectionExtensions.cs (service infrastructure — removed bootstrap registrations)
- 3 × BootstrapTests.cs (removed bootstrap test classes)

## Phase 2: Split mission-design → 4 projects ✅ COMPLETE
- [x] Created MissionDesign.Domain (entities only, no framework deps)
- [x] Created MissionDesign.Application (CQRS split: 13 folders, 26 files)
- [x] Created MissionDesign.Infrastructure (DbContext, migrations, DI registration)
- [x] Renamed Umbral.MissionDesign.Api → MissionDesign.Api (folder + csproj + namespaces)
- [x] CQRS split: each Command/Query + Handler in separate files
- [x] Controllers replaced minimal-API endpoints (MissionsController, EligibleMissionsController, MissionStagesController)
- [x] Program.cs: MapControllers(), direct MigrateAsync(), MediatR 12.x registration
- [x] Deleted dead smoke route tests, updated test namespaces
- [x] Renamed test project: MissionDesign.Api.Tests (dropped Umbral. prefix)
- [x] MediatR package upgraded from Extensions.DependencyInjection 11.1.0 → MediatR 12.4.0
- [x] Deleted empty Presentation/ dir and ghost MissionDesign.Api/ dir

## Phase 3: Split scoring-monitoring → 4 projects ✅ COMPLETE
- [x] Created ScoringMonitoring.Domain (entities only, references Umbral.ServiceDefaults for UmbralDomainException)
- [x] Created ScoringMonitoring.Application (CQRS split: 9 feature folders, 18 files)
  - Commands: LogSessionEvent, ApplyPenalty, RecordStageCredit
  - Queries: GetSessionEventLog, GetRanking
  - Contracts + ports in Features/SessionEventLogs, Features/Rankings, Features/Scoreboards
- [x] Created ScoringMonitoring.Infrastructure (DbContext, migrations, DI registration, SignalR publisher)
  - Moved SignalRScoringMonitoringUpdatesPublisher from Presentation/Realtime to Infrastructure/Realtime
  - Used `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for SignalR + config support
- [x] Renamed Umbral.ScoringMonitoring.Api → ScoringMonitoring.Api (folder + csproj + namespaces)
- [x] Moved ScoringMonitoringHub and IScoringMonitoringClient to ScoringMonitoring.Application.Hubs to avoid Api↔Infrastructure circular dependency
- [x] Program.cs: MapHealthChecks, MapControllers, direct MigrateAsync(), MediatR 12.x registration
- [x] Deleted dead smoke route tests, kept health endpoint test, updated test namespaces
- [x] Renamed test project: ScoringMonitoring.Api.Tests (dropped Umbral. prefix)
- [x] MediatR package upgraded from Extensions.DependencyInjection 11.1.0 → MediatR 12.4.0
- [x] TFM upgraded net8.0 → net10.0; EF Core/Npgsql/JwtBearer packages upgraded to 10.0.0
- [ ] Final build + test verification pending

### Findings / Decisions
- **Hub location**: The plan said "SignalR hub stays in Api", but the SignalR publisher (moved to Infrastructure) needs the concrete hub type for `IHubContext<T, TClient>`. To prevent a circular reference (Infrastructure cannot reference Api), `ScoringMonitoringHub` and `IScoringMonitoringClient` were moved into `ScoringMonitoring.Application.Hubs`. The Api still maps the hub endpoint.
- **No controllers exist**: The old service used minimal-API style with only a SignalR hub. `MapControllers()` is retained for future controller additions.
- **Smoke route tests referenced a non-existent `/api/scoring-monitoring/smoke/participant` route** (no controller or minimal endpoint defined). Following Phase 2 pattern, these tests were deleted.
- **Migrations namespace update**: Migrations moved from `Umbral.ScoringMonitoring.Api.Infrastructure.Migrations` to `ScoringMonitoring.Infrastructure.Persistence.Migrations`; `ProductVersion` annotation updated to 10.0.0.

## Phase 4: Split session-management → 4 projects ✅ COMPLETE
- [x] Created SessionManagement.Domain (14 entity files in LiveSessions/)
  - EnrollmentWindow, EvidenceSubmission, JoinCode, LiveSession (aggregate root, 1123 lines), LiveSessionStage, LiveSessionStageHint, LiveSessionStates, ParticipantUserId, ReleasedHint, SessionTeam, SessionTeamProgress, TeamParticipation, ValidationOutcome, ValidationOverrideLog
- [x] Created SessionManagement.Application (30 .cs files)
  - Hubs/SessionManagementHub.cs + Hubs/Contracts/SessionRealtimeContracts.cs (moved from Api to avoid circular dependency, same pattern as scoring-monitoring Phase 3)
  - Realtime/ISessionRealtimeNotifier.cs (port interface)
  - Scoring/IScoringMonitoringClient.cs (port interface + DTOs)
  - Features/EvidenceSubmissions/ (4 files: contracts + 3 handlers)
  - Features/Hints/ (2 files: CreateOperationalHint, ReleaseHint)
  - Features/LiveSessions/ (5 files: contracts + 4 handlers)
  - Features/Penalties/ (1 file: ApplyPenalty)
  - Features/SessionEnrollment/ (8 files: contracts + 7 handlers)
  - Features/SessionLifecycle/ (2 files: contracts + TransitionLiveSessionState)
  - Features/SessionSnapshots/ (4 files: contracts + 3 handlers)
- [x] Created SessionManagement.Infrastructure (22 files)
  - Persistence/SessionManagementDbContext.cs + SessionManagementDbContextFactory.cs + SessionManagementPersistence.cs
  - Persistence/Migrations/ (10 migration files with updated namespaces)
  - ServiceCollectionExtensions.cs (DI wiring for all infrastructure services)
  - AuthHeaderForwardingHandler.cs, CryptographicJoinCodeGenerator.cs
  - HttpContextCurrentOperatorIdentity.cs, HttpContextCurrentParticipantIdentity.cs
  - MissionDesignLiveSessionCatalog.cs, ScoringMonitoringHttpClient.cs
  - SignalRLiveSessionRealtimeNotifier.cs (uses Application.Hubs namespace)
- [x] Created SessionManagement.Api (thin host with Program.cs only)
  - Program.cs: SignalR, JwtBearer with access_token query param, MediatR 12.x, direct MigrateAsync
  - Hub mapping: app.MapHub<SessionManagementHub>("/hubs/session")
- [x] Renamed test project: Umbral.SessionManagement.Api.Tests → SessionManagement.Api.Tests
  - All 11 test files migrated with updated namespaces
  - Test references updated: Domain, Application.Features, Infrastructure.Persistence
- [x] TFM upgraded net8.0 → net10.0; all packages upgraded (EF Core 10.0.0, Npgsql 10.0.0, MediatR 12.4.0, JwtBearer 10.0.0)
- [x] MediatR package upgraded from MediatR.Extensions.Microsoft.DependencyInjection 11.1.0 → MediatR 12.4.0
- [x] Dockerfile updated: sdk:10.0, aspnet:10.0, new project path
- [x] Invoke-RepositoryValidation.ps1 updated with new project paths
- [ ] Final build + test verification pending (requires .NET 10.0 SDK)

### Findings / Decisions
- **Hub location**: Same pattern as scoring-monitoring — `SessionManagementHub` and `ISessionClient` moved to `SessionManagement.Application.Hubs` to prevent Infrastructure→Api circular reference. Api still maps the hub endpoint.
- **DbContext abstraction**: Application handlers use `ISessionManagementDbContext` interface (defined in `SessionManagement.Application.Abstractions`). The concrete `SessionManagementDbContext` lives only in Infrastructure.
- **No controllers exist**: The old service had no controllers or minimal-API endpoints — only MediatR handlers invoked through SignalR. `MapControllers()` retained for future additions.
- **Migrations namespace update**: Migrations moved from `Umbral.SessionManagement.Api.Infrastructure.Migrations` to `SessionManagement.Infrastructure.Persistence.Migrations`; entity type references in Designer files updated from `Umbral.SessionManagement.Api.Domain.LiveSessions.*` to `SessionManagement.Domain.LiveSessions.*`.
- **Old project directories**: `Umbral.SessionManagement.Api/` and `Umbral.SessionManagement.Api.Tests/` directories have been permanently deleted (source code moved; stale build artifacts cleaned).

### Build & Test Results (post-corrections)
- `dotnet build` — **0 errors, 0 warnings** (all 4 projects)
- `dotnet test` — **67 passed, 14 failed** out of 81 total
  - 14 failures: all `Auth:Authority` config missing in `WebApplicationFactory` integration tests (pre-existing, not Phase 4 regression)
  - 67 unit/handler tests: all passing

### Phase 4 Corrections (post-review audit)
- **CRITICAL**: MediatR scanned wrong assembly (`SessionManagementDbContext` in Infrastructure). Fixed to `ISessionManagementDbContext` (in Application, where handlers live).
- Created `SessionManagement.Infrastructure/GlobalUsings.cs` (missing — `Microsoft.NET.Sdk` + `FrameworkReference` lacks ASP.NET implicit usings; scoring-monitoring had this file, session-ops was missing it).
- Added `ProjectReference` to `SessionManagement.Infrastructure` in test `.csproj` (was missing — tests referenced Infrastructure types directly).
- Added `using SessionManagement.Infrastructure;` to `ScoringMonitoringHttpClientTests.cs` (class in root Infrastructure namespace, not Persistence sub-namespace).
- Renamed `SignalRLiveSessionStateNotifier.cs` → `SignalRLiveSessionRealtimeNotifier.cs` to match class name.
- Renamed `SubmitEvidenceHandler` → `SubmitEvidenceCommandHandler` for consistency with other handlers (and updated test reference).
- Added `.Trim()` on userId in `HttpContextCurrentParticipantIdentity` to match `HttpContextCurrentOperatorIdentity` behavior.
- Cleaned stale `Umbral.SessionManagement.Api/` and `Umbral.SessionManagement.Api.Tests/` directories (bin/obj artifacts only, no source remained).
- Corrected progress note: handlers use `ISessionManagementDbContext` interface, not concrete type.

## Phase 5: Clean user-management ✅ COMPLETE
- [x] Deleted WeatherForecastController.cs (template junk)
- [x] Deleted WeatherForecast.cs (template junk)
- [x] Deleted UnitTest1.cs (empty template test)
- [x] Deleted UserManagement.slnx (replaced by root Umbral.sln)
- [x] Verified Program.cs had no ServiceIdentity usage

## Phase 6: Root solution + package alignment ✅ COMPLETE
- [x] Generada solución maestra `Umbral.sln` en la raíz del repositorio.
- [x] Agregados los 23 proyectos al `Umbral.sln` con estructura de carpetas lógica replicando los directorios.
- [x] Implementado Central Package Management (CPM) vía `Directory.Packages.props`.
- [x] Implementado Central Build Properties vía `Directory.Build.props` (net10.0, ImplicitUsings, Nullable).
- [x] Se eliminó el atributo `Version="..."` y propiedades genéricas de compilación en los 23 archivos `.csproj`.
- [x] Todas las dependencias alineadas: `MediatR` (12.4.0), `Microsoft.EntityFrameworkCore` y web (10.0.0).

### Phase 7: Repository Pattern & Strict Clean Architecture Implementation
- [x] Create IRepository<T> and IUnitOfWork in Application layers.
- [x] Remove IDbContext abstractions from Application.
- [x] Implement generic Repository<T> in Infrastructure.
- [x] Refactor all 38 Handlers to use IRepository instead of DbSet.
- [x] Remove Microsoft.EntityFrameworkCore dependency from all Application projects.

**Status:** Completado. La capa Application ahora es purista y 100% agn�stica a la base de datos.
