---
name: clean-code
version: 0.2.0
description: Reviews and refactors code for readability, testability, maintainability, and structural cohesion using Clean Code principles plus this repo's engineering guardrails. Use when auditing project structure, identifying bad practices or code smells, reviewing module boundaries, improving naming/errors/tests, or refactoring code that has become hard to understand or change.
category: engineering
tags: [clean-code, refactoring, code-review, best-practices, maintainability, structure-review]
platforms:
  - claude-code
  - gemini-cli
  - openai-codex
  - mcp
license: MIT
maintainers:
  - github: maddhruv
---

# Clean Code

Use this skill for both code-level cleanup and repo/service-level maintainability reviews.

## Read first

1. `.agents/agents/operating-model.md`
2. `docs/architecture/repo-structure.md`
3. `docs/architecture/adr/2026-05-26-coding-standards-and-testability.md`
4. `docs/architecture/adr/2026-05-26-error-handling-and-boundary-design.md`
5. Relevant `src/*/CONTEXT.md` and ADRs for the bounded context being reviewed
6. `.agents/skills/engineering-guardrails/SKILL.md` when reviewing or changing code
7. `.agents/skills/local-validation/SKILL.md` when test evidence is needed

## Trigger this skill when

- The user asks for a code review focused on maintainability, readability, or good practices
- The user wants to review project structure, module boundaries, or folder responsibilities
- A service or feature feels hard to change, test, or reason about
- The user asks to identify code smells, technical debt, hidden dependencies, or weak error handling
- The user wants help refactoring names, functions, classes, modules, or tests

Do not use this skill as the main driver for product decisions, performance tuning, or cross-system architecture changes. For those, use the relevant architecture or diagnosis skill.

## Review workflow

1. Identify the scope: file, module, bounded context, or repo slice.
2. Read the repo structure and bounded-context language before judging names or layout.
3. Review in this order:
   - correctness risks from hidden dependencies or bad boundaries
   - testability and separation of decision logic from IO
   - cohesion, naming, and responsibility splits
   - duplication, dead abstractions, and readability issues
4. Prefer findings that explain why the current structure will slow future changes, not just how it looks.
5. If suggesting refactors, propose the smallest safe slice first and call out required tests.

## What to audit

### Code-level cleanliness

- Names reveal intent and use the bounded-context vocabulary
- Functions stay focused and avoid mixing abstraction levels
- Guard clauses flatten control flow where they improve clarity
- Boolean flags, nullable returns, and magic values are treated as smells
- Error handling is explicit and separates domain failures from technical failures
- Comments explain why, not what

### Structural quality

- Files and folders reflect real responsibilities instead of dumping unrelated behavior together
- Modules have clear seams and do not hide infrastructure access inside business logic
- Shared code lives in building blocks only when it is truly cross-cutting
- Application orchestration, domain rules, and infrastructure concerns are not collapsed into one class or file
- Dependencies point inward toward stable abstractions where possible
- New code follows the existing repo structure instead of inventing ad hoc folders

See `references/structure-review.md` for the full structural checklist.

## Refactoring rules

- Favor small, behavior-preserving refactors over broad rewrites
- Do not split functions or classes mechanically; extract only when the new name clarifies intent
- Do not introduce interfaces, factories, or layers without a concrete reason
- Ask for or add tests before structural refactors when behavior is not already protected
- When the problem is architectural rather than local, say so instead of forcing a Clean Code refactor

## Output format

When using this skill for review, respond with findings first.

1. List issues ordered by severity with file references when available.
2. Explain the concrete risk: harder testing, hidden coupling, mixed responsibilities, ambiguous names, fragile error handling, or boundary leakage.
3. After findings, include brief open questions or assumptions.
4. End with a short change plan or refactor direction only if useful.

If no meaningful findings are found, say that explicitly and mention any residual testing or structural blind spots.

## References

- `references/naming-guide.md`
- `references/code-smells.md`
- `references/solid-principles.md`
- `references/tdd.md`
- `references/structure-review.md`

Load only the reference files needed for the current task.
