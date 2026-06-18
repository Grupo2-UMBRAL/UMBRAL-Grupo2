# Handoff: Phase 3 — Split scoring-audit into 4 projects

## Current State
Phase 3 is functionally complete and verified. All scoring-audit code has been split into:

- `src/services/scoring-audit/ScoringAudit.Domain/` (class library, net10.0)
- `src/services/scoring-audit/ScoringAudit.Application/` (class library, net10.0)
- `src/services/scoring-audit/ScoringAudit.Infrastructure/` (class library, net10.0)
- `src/services/scoring-audit/ScoringAudit.Api/` (Web API, net10.0)
- `src/services/scoring-audit/ScoringAudit.Api.Tests/` (test project, net10.0)

The old `Umbral.ScoringAudit.Api/` directory has been removed.

## Build & Test Status
- `dotnet build` succeeds for all 4 scoring-audit projects.
- `dotnet test ScoringAudit.Api.Tests` passes: 33 tests, 0 failures.

## Key Architectural Decisions
1. **Hub location**: `ScoringAuditHub` and `IScoringAuditClient` live in `ScoringAudit.Application.Hubs` so the SignalR publisher (in Infrastructure) can reference the hub type without a circular dependency on the Api project.
2. **FrameworkReference**: Infrastructure uses `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for SignalR and configuration APIs instead of the obsolete `Microsoft.AspNetCore.SignalR` package.
3. **CQRS split**: Combined command/query files were split into separate `Command.cs`, `CommandHandler.cs`, `Query.cs`, `QueryHandler.cs`, plus request/response records.
4. **RankingProjection visibility**: Changed from `internal` to `public` because tests now live in a separate assembly.
5. **Dead smoke route tests removed**: The old `/api/scoring-audit/smoke/participant` route did not exist; tests were deleted and only the health endpoint test was kept.
6. **Migration strings updated**: Designer files and the model snapshot now reference `ScoringAudit.Domain.*` entity type names to match the moved domain classes.

## Files Updated Outside the Service
- `docs/refactoring/progress.md` — Phase 3 marked in progress, findings recorded.
- `scripts/Invoke-RepositoryValidation.ps1` — scoring-audit project paths updated to new names. Also fixed stale mission-design paths that Phase 2 missed.
- `src/services/README.md` — scoring-audit build/test/ef command paths updated.
- `infra/keycloak/docker/scoring-audit.Dockerfile` — paths and base image updated to net10.0.
- `src/services/scoring-audit/ScoringAudit.Api/Dockerfile` — paths and base image updated to net10.0.

## Remaining / Follow-up Work
1. **Run full backend validation**: `scripts/Invoke-RepositoryValidation.ps1 -Scope Backend` to confirm the script works end-to-end. MissionDesign paths were fixed, but verify no other stale paths remain.
2. **Phase 6 root solution**: There is still no root `Umbral.sln`. When Phase 6 is implemented, add the new scoring-audit projects and remove any old scoring-audit references.
3. **EF migration verification**: Consider running `dotnet ef migrations add EmptyVerify --project src/services/scoring-audit/ScoringAudit.Infrastructure/ScoringAudit.Infrastructure.csproj --startup-project src/services/scoring-audit/ScoringAudit.Api/ScoringAudit.Api.csproj` to ensure the model snapshot is consistent after the namespace move. Delete the empty migration afterwards if it produces no changes.
4. **Update progress.md**: Change Phase 3 status from 🔄 IN PROGRESS to ✅ COMPLETE once final validation is run.

## Suggested Skills for Next Session
- `local-validation` — to run the repository validation script correctly.
- `run-tests` — if further test runner issues appear.
- `dotnet-webapi` — if controllers/endpoints need to be added to the new Api project.

## References
- Plan: `docs/refactoring/plan.md`
- Progress tracker: `docs/refactoring/progress.md`
- Scoring-audit context: `src/services/scoring-audit/CONTEXT.md`
