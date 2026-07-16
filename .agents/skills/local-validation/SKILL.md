---
name: local-validation
description: Run the repo's reproducible validation scripts (Invoke-RepositoryValidation.ps1 / Invoke-ComposeSmokeValidation.ps1) before closing implementation, review, or bugfix work. Use when running local tests, picking the right validation scope, capturing evidence before merge, or explaining why validation could not run.
---

# Local Validation

`Invoke-RepositoryValidation.ps1` is the authoritative gate for code, backend coverage, and secret scanning; `Invoke-ComposeSmokeValidation.ps1` adds the `docker compose` stack smoke. Run the real script — never reassemble ad hoc test commands.

## The three tiers

Escalate only as far as the change reaches.

1. **Inner loop** — scoped to the touched area, fastest, run repeatedly while coding:
   `./scripts/Invoke-RepositoryValidation.ps1 -Scope Web -SkipComposeSmoke`
   Swap `-Scope` for `Mobile`, `Backend`, `Frontend`, `BackendUnit`, or `BackendIntegration`.
2. **Code gate** — repo-wide code/coverage/secrets, run once before close or merge:
   `./scripts/Invoke-RepositoryValidation.ps1 -SkipComposeSmoke`
3. **Compose smoke** — add only when the change touches integration, infra, or stack health:
   `./scripts/Invoke-RepositoryValidation.ps1` (or `Invoke-ComposeSmokeValidation.ps1` alone).

The GitHub Actions workflow wraps these same scripts — not a separate source of truth.

## Running honestly

- Claim pass only when the script exited pass. Read evidence from `temp/validation/` artifacts instead of pasting raw output.
- Compose smoke blocked by occupied ports or existing `umbral-*` containers: copy an env file, adjust ports, rerun with `-EnvironmentFilePath`. Report the block — never drop the smoke silently.
- No host `dotnet`: let the script use its container fallback.
- Mutation audit is a separate local tool, never part of these scripts nor a blocking CI threshold. See REFERENCE.md.

## Report

State: command run, result (pass / fail / blocked), key artifacts, coverage summary when present, and — if blocked — the cause plus the smallest unblocking next action.

See [REFERENCE.md](REFERENCE.md) for every scope, flag, artifact path, blocker, and the branch-coverage gate policy. Source of truth: `docs/architecture/validation-pipeline.md`.
