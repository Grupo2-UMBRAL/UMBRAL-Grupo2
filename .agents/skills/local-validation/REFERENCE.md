# Local Validation Reference

## Main commands

### Narrow validation by touched area

```powershell
./scripts/Invoke-RepositoryValidation.ps1 -Scope Web -SkipComposeSmoke
```

Every scope runs `Test-VersionedSecrets.ps1`, then only the selected area (`-Scope` values from `Invoke-RepositoryValidation.ps1`):

- `Web`: `npm ci`, lint, typecheck, build for `src/apps/web`
- `Mobile`: `npm ci`, typecheck, build for `src/apps/mobile`
- `Frontend`: `Web` + `Mobile`, no backend
- `Backend`: `.NET` build, all tests, coverage summary
- `BackendUnit`: `.NET` build + only unit test projects
- `BackendIntegration`: `.NET` only integration test projects
- `Full` (default): everything, plus compose smoke unless `-SkipComposeSmoke`

Compose smoke runs only under `-Scope Full`.

### Code validation without compose smoke

```powershell
./scripts/Invoke-RepositoryValidation.ps1 -SkipComposeSmoke
```

Runs:

- `Test-VersionedSecrets.ps1`
- `npm ci`, lint, typecheck, build for `src/apps/web`
- `npm ci`, typecheck, build for `src/apps/mobile`
- `.NET` build for service projects
- `.NET` test plus `XPlat Code Coverage` for backend test projects
- coverage summary to `temp/validation/backend-coverage-summary.json`

### Full validation

```powershell
./scripts/Invoke-RepositoryValidation.ps1
```

Adds compose smoke through `Invoke-ComposeSmokeValidation.ps1`.

### Compose smoke only

```powershell
./scripts/Invoke-ComposeSmokeValidation.ps1
```

Covers:

- `docker compose config`
- infra + backend stack `up -d --build`
- health endpoints
- Keycloak discovery
- auth smoke tests

### Mutation audit (local only)

Stryker.NET is an additional quality audit, not part of `Invoke-RepositoryValidation.ps1` and not a GitHub
Actions job initially. Restore the versioned local tool, then execute the configured audit for one production
project at a time. Consult `docs/architecture/validation-pipeline.md` for the required adoption order, scope and
report location.

Do not turn a mutation score into a blocking CI threshold before recording a baseline for the project being
audited.

## Useful flags

### Alternate env file for compose smoke

```powershell
./scripts/Invoke-ComposeSmokeValidation.ps1 -EnvironmentFilePath .env.validation
./scripts/Invoke-RepositoryValidation.ps1 -EnvironmentFilePath .env.validation
```

Use when default ports collide with another stack or Windows reserved ranges.

### Keep stack alive for manual inspection

```powershell
./scripts/Invoke-ComposeSmokeValidation.ps1 -LeaveRunning
```

### Coverage thresholds

```powershell
./scripts/Invoke-RepositoryValidation.ps1 -SkipComposeSmoke -TemporaryCoverageThreshold 10 -TargetCoverage 90
```

`Invoke-RepositoryValidation.ps1` defaults: `-TemporaryCoverageThreshold 10`, `-TargetCoverage 90`.

## Coverage gate policy

- Gated metric is **branch** coverage (`OverallBranchCoverage`), not line. Line coverage is reported as secondary reference only.
- `-TemporaryCoverageThreshold` (default `10`) is what breaks the build today.
- `-TargetCoverage` (default `90`) is the academic target reported alongside; raising the temporary threshold to `90` is the final step of the ramp-up plan.
- Aggregate is by totals (`Σ branches-covered / Σ branches-valid`), not per-project average. A project with `branches-valid = 0` counts as vacuous `100%`.

## Artifact paths

- `temp/validation/backend-coverage-summary.json`
- `temp/validation/TestResults/`
- `temp/validation/backend-coverage-report/index.html`
- `temp/validation/backend-coverage-report/Summary.txt`
- `temp/validation/mutation/<alcance>/`
- `temp/validation/compose/compose-config.txt`
- `temp/validation/compose/compose-up.txt`
- `temp/validation/compose/compose-ps.txt`
- `temp/validation/compose/auth-smoke-tests.txt`

## Known blockers to report explicitly

- existing `umbral-*` containers already occupying reserved container names
- required host ports already listening
- required host ports inside Windows excluded TCP ranges
- missing Docker daemon or network access during dependency/image restore
- failing secret scan or coverage gate

## Reporting template

- Command: `./scripts/Invoke-RepositoryValidation.ps1 -Scope Backend -SkipComposeSmoke`
- Result: passed / failed / blocked
- Evidence: artifact paths
- Coverage: overall percent and threshold status
- Blocker: exact cause if not complete
- Next action: smallest follow-up that would unblock validation
