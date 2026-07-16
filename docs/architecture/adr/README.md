# ADRs

Architecture Decision Records de UMBRAL.

Crear un ADR cuando una decisión sea:

- difícil de revertir
- sorprendente sin contexto
- resultado de un trade-off real

## Índice

| ADR | Título | Estado |
|---|---|---|
| [001](ADR-001-bounded-contexts.md) | Separar UMBRAL en cuatro bounded contexts | Accepted |
| [002](ADR-002-user-vs-session-team.md) | Separar `User` de `Session Team` | Accepted |
| [003](ADR-003-livesession-as-session-management-aggregate.md) | `LiveSession` como agregado raíz de session-management | Accepted |
| [004](ADR-004-scoreboard-source-of-truth.md) | `Scoreboard` como fuente de verdad del puntaje | Accepted |
| [005](ADR-005-per-team-progression.md) | Progresión de etapas por equipo | Accepted |
| [006](ADR-006-cqrs-single-database.md) | CQRS lógico sobre una única base de datos | Accepted |
| [007](ADR-007-keycloak-for-identity-and-access.md) | Keycloak como proveedor de identidad | Accepted |
| [008](ADR-008-signalr-for-real-time.md) | SignalR para tiempo real | Accepted |
| [009](ADR-009-mediatr-for-application-cqrs.md) | MediatR para la capa de aplicación (CQRS) | Accepted |
| [010](ADR-010-microservices-from-day-one.md) | Microservicios reales desde el día uno | Accepted |
| [011](ADR-011-hybrid-service-communication.md) | Comunicación híbrida (síncrona principal + RabbitMQ secundario) | Accepted |
| [012](ADR-012-service-internal-layering-and-shared-kernel-placement.md) | Capas internas por servicio y ubicación del shared kernel | Accepted (superseded en parte por 015) |
| [013](ADR-013-audit-domain-events-without-outbox.md) | Eventos de dominio de auditoría sin outbox | Superseded por 014 |
| [014](ADR-014-masstransit-transactional-outbox-for-audit.md) | MassTransit + outbox transaccional para auditoría | Accepted |
| [015](ADR-015-framework-free-shared-kernel.md) | Kernel compartido sin framework (`Umbral.Kernel`) | Accepted (supersede en parte a 012) |
| [016](ADR-016-mobile-participant-web-console.md) | Móvil = cliente del `Participant`; web = consola Admin/Operator | Accepted |
| [017](ADR-017-aggregate-stores-as-contract-repositories.md) | Stores por agregado como "repositorios por contratos" | Accepted (complementa 006) |
| [018](ADR-018-openapi-documents-behind-the-edge.md) | Documentos OpenAPI detrás del edge proxy | Accepted (complementa 015) |

### ADRs de estándares (sin numeración correlativa)

| Archivo | Tema |
|---|---|
| [2026-05-26-coding-standards-and-testability.md](2026-05-26-coding-standards-and-testability.md) | Estándares de código y testabilidad |
| [2026-05-26-error-handling-and-boundary-design.md](2026-05-26-error-handling-and-boundary-design.md) | Manejo de errores y diseño de bordes |
