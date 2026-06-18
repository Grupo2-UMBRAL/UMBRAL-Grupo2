# Handoff: Phase 3 â€” Split scoring-monitoring into 4 projects

## Current State
Phase 3 is functionally complete and verified. All scoring-monitoring code has been split into:

- `src/services/scoring-monitoring/ScoringMonitoring.Domain/` (class library, net10.0)
- `src/services/scoring-monitoring/ScoringMonitoring.Application/` (class library, net10.0)
- `src/services/scoring-monitoring/ScoringMonitoring.Infrastructure/` (class library, net10.0)
- `src/services/scoring-monitoring/ScoringMonitoring.Api/` (Web API, net10.0)
- `src/services/scoring-monitoring/ScoringMonitoring.Api.Tests/` (test project, net10.0)

The old `Umbral.ScoringMonitoring.Api/` directory has been removed.

## Build & Test Status
- `dotnet build` succeeds for all 4 scoring-monitoring projects.
- `dotnet test ScoringMonitoring.Api.Tests` passes: 33 tests, 0 failures.

## Key Architectural Decisions
1. **Hub location**: `ScoringMonitoringHub` and `IScoringMonitoringClient` live in `ScoringMonitoring.Application.Hubs` so the SignalR publisher (in Infrastructure) can reference the hub type without a circular dependency on the Api project.
2. **FrameworkReference**: Infrastructure uses `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for SignalR and configuration APIs instead of the obsolete `Microsoft.AspNetCore.SignalR` package.
3. **CQRS split**: Combined command/query files were split into separate `Command.cs`, `CommandHandler.cs`, `Query.cs`, `QueryHandler.cs`, plus request/response records.
4. **RankingProjection visibility**: Changed from `internal` to `public` because tests now live in a separate assembly.
5. **Dead smoke route tests removed**: The old `/api/scoring-monitoring/smoke/participant` route did not exist; tests were deleted and only the health endpoint test was kept.
6. **Migration strings updated**: Designer files and the model snapshot now reference `ScoringMonitoring.Domain.*` entity type names to match the moved domain classes.

## Files Updated Outside the Service
- `docs/refactoring/progress.md` â€” Phase 3 marked in progress, findings recorded.
- `scripts/Invoke-RepositoryValidation.ps1` â€” scoring-monitoring project paths updated to new names. Also fixed stale mission-design paths that Phase 2 missed.
- `src/services/README.md` â€” scoring-monitoring build/test/ef command paths updated.
- `infra/keycloak/docker/scoring-monitoring.Dockerfile` â€” paths and base image updated to net10.0.
- `src/services/scoring-monitoring/ScoringMonitoring.Api/Dockerfile` â€” paths and base image updated to net10.0.

## Remaining / Follow-up Work
1. **Run full backend validation**: `scripts/Invoke-RepositoryValidation.ps1 -Scope Backend` to confirm the script works end-to-end. MissionDesign paths were fixed, but verify no other stale paths remain.
2. **Phase 6 root solution**: There is still no root `Umbral.sln`. When Phase 6 is implemented, add the new scoring-monitoring projects and remove any old scoring-monitoring references.
3. **EF migration verification**: Consider running `dotnet ef migrations add EmptyVerify --project src/services/scoring-monitoring/ScoringMonitoring.Infrastructure/ScoringMonitoring.Infrastructure.csproj --startup-project src/services/scoring-monitoring/ScoringMonitoring.Api/ScoringMonitoring.Api.csproj` to ensure the model snapshot is consistent after the namespace move. Delete the empty migration afterwards if it produces no changes.
4. **Update progress.md**: Change Phase 3 status from ðŸ”„ IN PROGRESS to âœ… COMPLETE once final validation is run.

## Suggested Skills for Next Session
- `local-validation` â€” to run the repository validation script correctly.
- `run-tests` â€” if further test runner issues appear.
- `dotnet-webapi` â€” if controllers/endpoints need to be added to the new Api project.

## References
- Plan: `docs/refactoring/plan.md`
- Progress tracker: `docs/refactoring/progress.md`
- Scoring-audit context: `src/services/scoring-monitoring/CONTEXT.md`
