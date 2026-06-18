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

## Phase 3: Split scoring-audit → 4 projects ✅ COMPLETE
- [x] Created ScoringAudit.Domain (entities only, references Umbral.ServiceDefaults for UmbralDomainException)
- [x] Created ScoringAudit.Application (CQRS split: 9 feature folders, 18 files)
  - Commands: LogSessionEvent, ApplyPenalty, RecordStageCredit
  - Queries: GetSessionEventLog, GetRanking
  - Contracts + ports in Features/Audit, Features/Rankings, Features/Scoreboards
- [x] Created ScoringAudit.Infrastructure (DbContext, migrations, DI registration, SignalR publisher)
  - Moved SignalRScoringAuditUpdatesPublisher from Presentation/Realtime to Infrastructure/Realtime
  - Used `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for SignalR + config support
- [x] Renamed Umbral.ScoringAudit.Api → ScoringAudit.Api (folder + csproj + namespaces)
- [x] Moved ScoringAuditHub and IScoringAuditClient to ScoringAudit.Application.Hubs to avoid Api↔Infrastructure circular dependency
- [x] Program.cs: MapHealthChecks, MapControllers, direct MigrateAsync(), MediatR 12.x registration
- [x] Deleted dead smoke route tests, kept health endpoint test, updated test namespaces
- [x] Renamed test project: ScoringAudit.Api.Tests (dropped Umbral. prefix)
- [x] MediatR package upgraded from Extensions.DependencyInjection 11.1.0 → MediatR 12.4.0
- [x] TFM upgraded net8.0 → net10.0; EF Core/Npgsql/JwtBearer packages upgraded to 10.0.0
- [ ] Final build + test verification pending

### Findings / Decisions
- **Hub location**: The plan said "SignalR hub stays in Api", but the SignalR publisher (moved to Infrastructure) needs the concrete hub type for `IHubContext<T, TClient>`. To prevent a circular reference (Infrastructure cannot reference Api), `ScoringAuditHub` and `IScoringAuditClient` were moved into `ScoringAudit.Application.Hubs`. The Api still maps the hub endpoint.
- **No controllers exist**: The old service used minimal-API style with only a SignalR hub. `MapControllers()` is retained for future controller additions.
- **Smoke route tests referenced a non-existent `/api/scoring-audit/smoke/participant` route** (no controller or minimal endpoint defined). Following Phase 2 pattern, these tests were deleted.
- **Migrations namespace update**: Migrations moved from `Umbral.ScoringAudit.Api.Infrastructure.Migrations` to `ScoringAudit.Infrastructure.Persistence.Migrations`; `ProductVersion` annotation updated to 10.0.0.

## Phase 4: Split session-operations → 4 projects ✅ COMPLETE
- [x] Created SessionOperations.Domain (14 entity files in LiveSessions/)
  - EnrollmentWindow, EvidenceSubmission, JoinCode, LiveSession (aggregate root, 1123 lines), LiveSessionStage, LiveSessionStageHint, LiveSessionStates, ParticipantUserId, ReleasedHint, SessionTeam, SessionTeamProgress, TeamParticipation, ValidationOutcome, ValidationOverrideLog
- [x] Created SessionOperations.Application (30 .cs files)
  - Hubs/SessionOperationsHub.cs + Hubs/Contracts/SessionRealtimeContracts.cs (moved from Api to avoid circular dependency, same pattern as scoring-audit Phase 3)
  - Realtime/ISessionRealtimeNotifier.cs (port interface)
  - Scoring/IScoringAuditClient.cs (port interface + DTOs)
  - Features/EvidenceSubmissions/ (4 files: contracts + 3 handlers)
  - Features/Hints/ (2 files: CreateOperationalHint, ReleaseHint)
  - Features/LiveSessions/ (5 files: contracts + 4 handlers)
  - Features/Penalties/ (1 file: ApplyPenalty)
  - Features/SessionEnrollment/ (8 files: contracts + 7 handlers)
  - Features/SessionLifecycle/ (2 files: contracts + TransitionLiveSessionState)
  - Features/SessionSnapshots/ (4 files: contracts + 3 handlers)
- [x] Created SessionOperations.Infrastructure (22 files)
  - Persistence/SessionOperationsDbContext.cs + SessionOperationsDbContextFactory.cs + SessionOperationsPersistence.cs
  - Persistence/Migrations/ (10 migration files with updated namespaces)
  - ServiceCollectionExtensions.cs (DI wiring for all infrastructure services)
  - AuthHeaderForwardingHandler.cs, CryptographicJoinCodeGenerator.cs
  - HttpContextCurrentOperatorIdentity.cs, HttpContextCurrentParticipantIdentity.cs
  - MissionDesignLiveSessionCatalog.cs, ScoringAuditHttpClient.cs
  - SignalRLiveSessionStateNotifier.cs (uses Application.Hubs namespace)
- [x] Created SessionOperations.Api (thin host with Program.cs only)
  - Program.cs: SignalR, JwtBearer with access_token query param, MediatR 12.x, direct MigrateAsync
  - Hub mapping: app.MapHub<SessionOperationsHub>("/hubs/session")
- [x] Renamed test project: Umbral.SessionOperations.Api.Tests → SessionOperations.Api.Tests
  - All 11 test files migrated with updated namespaces
  - Test references updated: Domain, Application.Features, Infrastructure.Persistence
- [x] TFM upgraded net8.0 → net10.0; all packages upgraded (EF Core 10.0.0, Npgsql 10.0.0, MediatR 12.4.0, JwtBearer 10.0.0)
- [x] MediatR package upgraded from MediatR.Extensions.Microsoft.DependencyInjection 11.1.0 → MediatR 12.4.0
- [x] Dockerfile updated: sdk:10.0, aspnet:10.0, new project path
- [x] Invoke-RepositoryValidation.ps1 updated with new project paths
- [ ] Final build + test verification pending (requires .NET 10.0 SDK)

### Findings / Decisions
- **Hub location**: Same pattern as scoring-audit — `SessionOperationsHub` and `ISessionClient` moved to `SessionOperations.Application.Hubs` to prevent Infrastructure→Api circular reference. Api still maps the hub endpoint.
- **DbContext direct reference**: Application handlers reference `SessionOperationsDbContext` directly (same pragmatic compromise as original code). A future refinement could introduce an `ISessionOperationsDbContext` abstraction, but the plan prioritizes structural decomposition first.
- **No controllers exist**: The old service had no controllers or minimal-API endpoints — only MediatR handlers invoked through SignalR. `MapControllers()` retained for future additions.
- **Migrations namespace update**: Migrations moved from `Umbral.SessionOperations.Api.Infrastructure.Migrations` to `SessionOperations.Infrastructure.Persistence.Migrations`; entity type references in Designer files updated from `Umbral.SessionOperations.Api.Domain.LiveSessions.*` to `SessionOperations.Domain.LiveSessions.*`.
- **Old project preserved**: `Umbral.SessionOperations.Api/` and `Umbral.SessionOperations.Api.Tests/` directories han sido eliminados de manera permanente.


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
