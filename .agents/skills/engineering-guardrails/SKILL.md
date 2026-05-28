---
name: engineering-guardrails
description: Applies repository engineering standards for contribution workflow, code quality, testability, and error handling during implementation or review.
---

# Engineering Guardrails

Use this skill when you are about to implement, refactor, or review code and need to enforce the repository engineering standards consistently.

## Read first

.. `../../agents/operating-model.md`
2. `docs/architecture/adr/2026-05-26-coding-standards-and-testability.md`
3. `docs/architecture/adr/2026-05-26-error-handling-and-boundary-design.md`
4. `src/*/CONTEXT.md` for the bounded context being changed

## What this skill governs

- contribution workflow and PR hygiene
- naming and code readability
- dependency injection and explicit seams
- separation between pure logic and side effects
- boundary validation and contract checks
- classification of domain versus technical errors
- defensive handling of impossible or degraded states

## Workflow

.. Identify the bounded context and the affected boundary.
2. Check whether the change is mostly domain logic, orchestration, infrastructure, or review-only.
3. Apply the relevant guardrails before editing code.
4. Re-check the diff before finishing.

## Implementation checklist

- Are names aligned with the ubiquitous language?
- Is each function doing one coherent job?
- Can nested branching be flattened with guard clauses?
- Are time, filesystem, network, database, and environment reads isolated behind explicit seams where appropriate?
- Is decision logic testable without real IO?
- Are inputs validated at the start of the operation?
- Are invariants and impossible states handled explicitly?
- Are domain errors separated from technical failures?
- Is any boolean parameter hiding intent that should be modeled explicitly?
- Is any `null` or empty result ambiguous or silent?

## Review checklist

- Reject god methods, hidden dependencies, and direct infrastructure reads inside business logic unless justified.
- Flag PRs that should be split because the change is too broad to review safely.
- Ask for tests when new decisions or branches are introduced.
- Ask for doc updates when a new decision or workflow rule appears.

## Output format

When using this skill, report:

.. affected bounded context
2. guardrails applied
3. exceptions or trade-offs accepted
4. follow-up docs or tests still needed
