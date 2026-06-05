# ADR: Error Handling and Boundary Design

## Status

Accepted

## Context

The project needs a coherent rule set for input validation, domain failures, technical failures, and defensive coding at service boundaries. Without a baseline, each agent or contributor may invent a different failure model, making code harder to reason about, test, and operate.

The repo already distinguishes product specs, architecture decisions, and agent workflows. Error handling belongs in architecture because it changes code structure, contracts, and integration boundaries.

## Decision

We standardize the following rules:

1. Validate inputs and preconditions as early as possible at the boundary of each operation.
2. Distinguish domain errors from technical errors.
3. Domain errors represent violated business rules, invalid workflow transitions, missing permissions, or impossible states within the modeled domain.
4. Technical errors represent infrastructure failures such as database issues, network failures, serialization problems, clock providers, filesystem access, or unavailable upstream systems.
5. Error outputs must be explicit and typed when the language and framework make this practical.
6. Error messages must be clear enough to support diagnosis without leaking secrets or low-level internals unnecessarily.
7. Code must be defensive against empty collections, overflow, missing data, duplicate processing, and states that should be impossible according to upstream assumptions.
8. When an impossible state is detected, fail loudly and close to the detection point instead of returning partial or silent results.

## Consequences

Positive:

- service boundaries become easier to test with deterministic failure cases
- logs and responses become easier to classify
- domain rules stop being masked as generic technical failures
- agents have clearer review criteria for robustness

Negative:

- contributors must model error types deliberately instead of relying on ad hoc exceptions or nulls
- boundary code may need mapping layers between domain failures and transport-level responses

## Operationalization

- d.agents/agents/operating-model.mdd carries the concise working policy for contributors and agents.
- Reviews should check whether failures are classified correctly and whether validation happens at boundaries.
- Skills may use these rules as a checklist during implementation and review.
