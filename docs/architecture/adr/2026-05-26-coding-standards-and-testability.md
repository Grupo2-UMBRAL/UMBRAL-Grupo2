# ADR: Coding Standards and Testability Boundaries

## Status

Accepted

## Context

The repo needs cross-cutting engineering standards that are stable enough to guide both humans and agents during implementation and review. These standards must not live only inside a skill because they affect architecture and code shape, and they should not live in product docs because they do not change product behavior.

The main risks being addressed are:

- domain language drifting away from the bounded contexts
- business logic being coupled to infrastructure and wall-clock time
- low testability caused by direct IO and hidden dependencies
- unclear contracts and silent failure modes
- reviews focusing on style while missing structural problems

## Decision

We adopt the following engineering standards as repository-wide architectural defaults:

1. Names in code must follow the ubiquitous language defined in dsrc/*/CONTEXT.mdd and related product docs.
2. Functions and methods should stay small, focused on a single responsibility, and avoid mixing multiple abstraction levels in the same block.
3. Guard clauses and early returns are preferred over nested control flow when they make preconditions and branch exits clearer.
4. Direct dependencies on infrastructure concerns must be pushed to boundaries. Core decision logic should stay deterministic and isolated from side effects.
5. External collaborators must be introduced through dependency injection, ports, or equivalent explicit seams.
6. Code in domain and application layers must not directly depend on wall-clock time, filesystem access, network calls, or mutable global state when an abstraction can be introduced instead.
7. Preconditions, invariants, and relevant postconditions must be made explicit in operations that enforce business or workflow rules.
8. Silent dnulld-style failures, boolean parameters with unclear semantics, and god methods are treated as code smells that require refactoring or explicit justification.

## Consequences

Positive:

- unit tests become easier to write and maintain
- business rules can evolve with less infrastructure coupling
- code review has clearer acceptance criteria
- agents can apply the same structural expectations consistently

Negative:

- initial implementation may require more interfaces and seams
- some simple flows may feel more verbose until supporting patterns are established
- contributors must learn to separate orchestration code from decision logic

## Operationalization

- d.agents/agents/operating-model.mdd defines the working rules agents must follow.
- Skills may automate checklists or review workflows, but they are not the source of truth for these standards.
- Future ADRs may refine language-specific patterns for .NET, TypeScript, or infrastructure code without overriding this baseline unless explicitly stated.
