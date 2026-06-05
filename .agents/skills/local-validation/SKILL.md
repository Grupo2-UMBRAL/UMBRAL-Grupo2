---
name: local-validation
description: Guides agents through the repo's reproducible local validation commands for code checks, backend coverage, secret scanning, and docker-compose smoke tests. Use when an agent needs to run local tests, choose the right validation command, capture evidence before closing a change, or explain why local validation could not run.
---

# Local Validation

Use repo validation scripts before closing implementation, review, or bugfix work.

## Read first

1. `docs/architecture/validation-pipeline.md`
2. `scripts/Invoke-RepositoryValidation.ps1`
3. `scripts/Invoke-ComposeSmokeValidation.ps1`

## Quick start

- Narrow loop by touched area:
  `./scripts/Invoke-RepositoryValidation.ps1 -Scope Web -SkipComposeSmoke`
  `./scripts/Invoke-RepositoryValidation.ps1 -Scope Mobile -SkipComposeSmoke`
  `./scripts/Invoke-RepositoryValidation.ps1 -Scope Backend -SkipComposeSmoke`
- Code, build, tests, coverage, secrets across repo:
  `./scripts/Invoke-RepositoryValidation.ps1 -SkipComposeSmoke`
- Full local validation including compose smoke:
  `./scripts/Invoke-RepositoryValidation.ps1`
- Compose smoke only:
  `./scripts/Invoke-ComposeSmokeValidation.ps1`

## Workflow

1. Pick the narrowest useful command.
2. During implementation, prefer `-Scope Web`, `-Scope Mobile`, or `-Scope Backend` over repo-wide validation.
3. Run the real repo script instead of reassembling ad hoc test commands.
4. Before close or integration, run the full code gate once with `./scripts/Invoke-RepositoryValidation.ps1 -SkipComposeSmoke`.
5. If compose smoke is blocked by occupied ports or existing `umbral-*` containers, create an alternate env file and rerun with `-EnvironmentFilePath`.
6. Read artifacts from `temp/validation/` instead of pasting long raw output.
7. Report exactly what ran, what passed, what failed, and what stayed blocked.
8. When acting as orchestrator or final integrator, prefer `Invoke-RepositoryValidation.ps1` as the authoritative pre-merge validation entrypoint.

## Rules

- Do not claim tests passed unless the script or command actually passed.
- Do not silently skip compose smoke; say why it was skipped or blocked.
- Prefer scoped `Invoke-RepositoryValidation.ps1` during the inner loop, then one repo-wide `Invoke-RepositoryValidation.ps1 -SkipComposeSmoke` before merge.
- Treat the GitHub Actions workflow as a CI wrapper around the same repo scripts, not as a separate source of truth.
- If host lacks `dotnet`, let the script use its container fallback.
- If output is noisy, summarize and point to artifact files.

## Output

Report:

1. command run
2. result
3. key artifacts
4. coverage summary when present
5. blocker and next action when validation could not complete

See [REFERENCE.md](REFERENCE.md) for flags, blockers, and artifact paths.
