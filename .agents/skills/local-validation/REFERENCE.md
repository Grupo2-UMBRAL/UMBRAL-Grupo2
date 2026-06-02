# Local Validation Reference

## Main commands

### Narrow validation by touched area

```powershell
./scripts/Invoke-RepositoryValidation.ps1 -Scope Web -SkipComposeSmoke
./scripts/Invoke-RepositoryValidation.ps1 -Scope Mobile -SkipComposeSmoke
./scripts/Invoke-RepositoryValidation.ps1 -Scope Backend -SkipComposeSmoke
```

Runs `Test-VersionedSecrets.ps1` plus only selected area:

- `Web`: `npm ci`, lint, typecheck, build for `src/apps/web`
- `Mobile`: `npm ci`, typecheck, build for `src/apps/mobile`
- `Backend`: `.NET` build, tests, coverage summary

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

## Artifact paths

- `temp/validation/backend-coverage-summary.json`
- `temp/validation/TestResults/`
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
