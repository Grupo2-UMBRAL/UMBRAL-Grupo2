# Structure Review

Use this checklist when the user wants to review a project's structure, module quality, or lack of good practices beyond single functions.

## Scope first

Decide whether you are reviewing:

- one file
- one module or service
- one bounded context
- a cross-cutting repo slice

Do not mix all scopes in one pass unless the user asked for a repo-wide audit.

## Structural smells

Flag these first because they usually create the most maintenance cost:

- God modules: one file or class owns orchestration, rules, persistence, mapping, and transport concerns
- Boundary leakage: domain/application code reads time, filesystem, network, framework state, or environment directly
- Responsibility drift: a folder or project contains concepts from multiple bounded contexts without a clear reason
- Shotgun surgery: one small rule change requires edits across many unrelated files
- Inward instability: low-level details dictate high-level flow instead of being isolated behind seams
- Fake sharing: code moved to shared/building-blocks too early, causing coupling between services
- Transport-driven domain: request/response DTOs or framework handlers become the real domain model
- Test-hostile design: behavior cannot be tested without containers, databases, clocks, or network access

## Repo-structure checks

- Does the code live in the folder implied by `docs/architecture/repo-structure.md`?
- Does the change preserve bounded-context vocabulary from `src/services/*/CONTEXT.md`?
- Is shared code actually cross-cutting, or should it move back into one service?
- Are new folders justified, or are they compensating for unclear module responsibilities?
- Are implementation details pushed to edges such as infrastructure, adapters, handlers, or clients?

## Good-practice checks

- Names are specific enough to reveal business intent
- Public APIs are smaller and clearer than internal helpers
- Data validation happens near boundaries
- Domain errors and technical failures are not conflated
- Side effects are isolated from decision logic
- Nulls, booleans, and generic `Manager` or `Helper` types are challenged
- Comments are sparse and useful
- Tests protect behavior, not implementation details

## Review heuristics

When writing findings, prefer statements like:

- "This handler mixes validation, authorization, persistence, and response mapping, so every rule change now touches transport code."
- "This shared utility is only used by one bounded context, so placing it in building blocks increases coupling without reuse."
- "This service reads the clock directly, which makes the rule hard to test deterministically."

Avoid vague findings like:

- "Code could be cleaner."
- "Consider SOLID."
- "Maybe refactor this."

## Refactor directions

Typical safe moves:

- Extract pure decision logic from handlers or controllers
- Move infrastructure calls behind explicit collaborators
- Split one module by responsibility, not by arbitrary file count
- Rename types to match bounded-context language
- Pull boundary validation to the start of the flow
- Replace nullable or flag-driven flows with explicit result or command models

Typical unsafe moves:

- Introducing new layers without a real boundary problem
- Moving code into shared libraries before a second concrete use case exists
- Reorganizing the whole repo to satisfy a style preference
- Splitting every long function into tiny helpers without improving readability
