# ADR-012: Service Internal Layering and Shared Kernel Placement

## Status

Accepted

## Context

The repository already decided to start with real microservices, `Keycloak` for identity, and hybrid service communication. What is still ambiguous is the internal target shape of each backend service during this phase of the project.

Without an explicit target, different agents could:

- introduce different layer names per service
- let `Application` depend on `Infrastructure`
- split each service into many `.csproj` files before that complexity is justified
- keep growing `src/services/building-blocks/` as if it were a domain bucket
- assume that the gateway owns authorization once it becomes the public edge

Those choices are difficult to unwind later and would make follow-up refactors drift in incompatible directions.

## Decision

We standardize the current target architecture for backend services as follows:

1. Each bounded-context service keeps a single `.Api` project during this phase.
2. Inside that single project, the target internal structure is:
   - `Presentation`
   - `Application`
   - `Domain`
   - `Infrastructure`
3. `Presentation` contains transport-facing concerns such as HTTP endpoints, auth boundary adapters, and realtime hubs.
4. `Application` coordinates use cases and may depend on `Domain`, but it must not depend on `Infrastructure`.
5. `Infrastructure` implements technical details required by the inner layers, including persistence, external clients, messaging adapters, and framework wiring.
6. The public gateway remains an edge concern for unified entry, routing, and client-facing composition, but it does not become the owner of business authorization.
7. Every backend service must still validate `JWT` tokens and enforce its own authorization rules at its own boundary.
8. Shared technical code that is not part of a bounded context moves out of `src/services/building-blocks/` and belongs under `src/shared`.
9. `src/shared` is reserved for technical cross-cutting support. It must not absorb business concepts, bounded-context contracts, or domain models.

## Consequences

Positive:

- follow-up refactors now have a stable layering target without forcing an immediate multi-project split
- services keep clear ownership of their own auth and authorization boundaries
- `SignalR` hubs and other transport adapters have a defined home in `Presentation`
- shared technical code stops competing with bounded contexts for space under `src/services`

Negative:

- the repository will temporarily carry a target structure that may be only partially implemented
- contributors must distinguish between "single project for now" and "no layering discipline"
- future extraction into multiple projects, if it ever happens, must preserve the same dependency direction

## Guardrails

- Treat `Application -> Infrastructure` as a target violation.
- Do not read this ADR as a mandate to create new `.csproj` files right now.
- Do not move business contracts, aggregates, or ubiquitous-language concepts into `src/shared`.
- Do not centralize business authorization in the gateway.

## Related decisions

- `ADR-007` keeps identity delegated to `Keycloak`, while each service still authorizes its own actions.
- `ADR-010` keeps service ownership aligned with bounded contexts.
- `ADR-011` keeps the main business flow under service ownership instead of pushing control outward to the edge.
