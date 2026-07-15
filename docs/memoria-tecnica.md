# Memoria Técnica — UMBRAL

> Documento de defensa técnica de la entrega final. Reúne las decisiones de arquitectura, el
> modelo del dominio, los patrones implementados, la estrategia de pruebas, los hallazgos de
> refactorización y las instrucciones para levantar la solución en ambiente local.
>
> Es también el **índice** de la evidencia: cada afirmación enlaza al ADR, documento o archivo
> real que la sustenta. Verificado sobre `develop`.

---

## 0. Cómo leer este documento

| Si buscas… | Ve a |
|---|---|
| Qué es UMBRAL y su alcance | [§1](#1-resumen-de-la-solución) |
| Decisiones de arquitectura y por qué | [§2](#2-decisiones-de-arquitectura) · [ADRs](architecture/adr/README.md) |
| Modelo del dominio (agregados, VOs, eventos) | [§3](#3-modelo-del-dominio) · [glosario](product/glossary.md) |
| Patrones de diseño con evidencia | [§4](#4-patrones-de-diseño) · [patterns-map](architecture/patterns-map.md) |
| Justificación SOLID | [§5](#5-principios-solid) |
| Logging, excepciones, validación, seguridad | [§6](#6-cross-cutting-concerns) |
| Pruebas y cobertura | [§7](#7-pruebas-y-cobertura) |
| Flujo RabbitMQ y tiempo real | [§8](#8-mensajería-rabbitmq) · [§9](#9-tiempo-real-signalr) |
| Pipeline de CI | [§10](#10-integración-continua) · [validation-pipeline](architecture/validation-pipeline.md) |
| **Levantar en local** | [§11](#11-ejecución-local) · [local-infrastructure](architecture/local-infrastructure.md) |
| Refactorizaciones | [§12](#12-refactorizaciones) · [docs/refactoring](refactoring/) |
| **Trazabilidad rúbrica → evidencia** | [§13](#13-trazabilidad-rúbrica--evidencia) |
| Qué falta / honestidad técnica | [§14](#14-estado-y-pendientes) |

---

## 1. Resumen de la solución

UMBRAL es un juego de misiones inmersivas en tiempo real (búsqueda del tesoro + trivia) operado
por un panel de administración/operación y jugado desde clientes móviles. La solución se compone de:

- **4 microservicios .NET** (bounded contexts): `user-management`, `mission-management`,
  `session-management`, `scoring-monitoring`.
- **Edge-proxy** (`Umbral.EdgeProxy`, YARP) como único punto de entrada local.
- **Consola web** (React 19 / Vite) para Admin y Operador.
- **Cliente móvil** (Expo / React Native) para el Participante.
- **Infraestructura**: Postgres 16, RabbitMQ 3.13, Keycloak 25, todo orquestado con Docker Compose.

Comunicación: **HTTP síncrono** como canal primario y **RabbitMQ (MassTransit)** asíncrono para
auditoría; **SignalR** para actualización en vivo. Ver [ADR-010](architecture/adr/ADR-010-microservices-from-day-one.md),
[ADR-011](architecture/adr/ADR-011-hybrid-service-communication.md).

---

## 2. Decisiones de arquitectura

Todas las decisiones estructurales están registradas como ADRs en
[`docs/architecture/adr/`](architecture/adr/README.md). Las más relevantes para la defensa:

| Decisión | ADR | Resumen |
|---|---|---|
| Cuatro bounded contexts | [ADR-001](architecture/adr/ADR-001-bounded-contexts.md) | Un servicio por contexto; frontera de lenguaje y de datos |
| Microservicios desde el día uno | [ADR-010](architecture/adr/ADR-010-microservices-from-day-one.md) | Servicios reales, no un monolito modular |
| Capas internas por servicio | [ADR-012](architecture/adr/ADR-012-service-internal-layering-and-shared-kernel-placement.md) | `.Api / .Application / .Domain / .Infrastructure`; nada de contratos de negocio en `src/shared` |
| Kernel sin framework | [ADR-015](architecture/adr/ADR-015-framework-free-shared-kernel.md) | `Umbral.Kernel` (puro) vs `Umbral.ServiceDefaults` (wiring) |
| CQRS sobre una sola BD | [ADR-006](architecture/adr/ADR-006-cqrs-single-database.md) | CQRS lógico con MediatR; un Postgres con esquemas separados |
| MediatR para la capa de aplicación | [ADR-009](architecture/adr/ADR-009-mediatr-for-application-cqrs.md) | Commands/queries + handlers + pipeline behaviors |
| Keycloak para identidad | [ADR-007](architecture/adr/ADR-007-keycloak-for-identity-and-access.md) | OIDC/JWT; roles Administrator/Operator/Participant |
| SignalR para tiempo real | [ADR-008](architecture/adr/ADR-008-signalr-for-real-time.md) | Hub de sesión; token por query string en WebSocket |
| Outbox transaccional para auditoría | [ADR-014](architecture/adr/ADR-014-masstransit-transactional-outbox-for-audit.md) | MassTransit + EF Core outbox, at-least-once (**supera** a [ADR-013](architecture/adr/ADR-013-audit-domain-events-without-outbox.md)) |
| Stores por agregado como repositorios | [ADR-017](architecture/adr/ADR-017-aggregate-stores-as-contract-repositories.md) | Contrato de persistencia acotado al agregado |

### Arquitectura limpia / hexagonal

Cada servicio separa dominio, aplicación e infraestructura, con la **dependencia apuntando hacia
adentro**. Esta invariante **no se confía a la disciplina, se verifica con un test**:
`src/shared/Umbral.ServiceDefaults.UnitTests/ArchitectureInvariantsTests.cs` afirma que todo
proyecto `*.Domain` referencia únicamente `Umbral.Kernel` y jamás un paquete de framework.

- **Puertos** en `Application/Abstractions` (interfaces): `ILiveSessionRepository`,
  `ISessionRealtimeNotifier`, `IMissionStore`, `IOperatorAdministrationPort`, …
- **Adaptadores** en `Infrastructure`/`Api`: `LiveSessionRepository`,
  `SignalRLiveSessionRealtimeNotifier`, `MissionStore`, `KeycloakAdminApiClient`, …

### Edge-proxy como frontera local

`src/apps/edge-proxy` (YARP) expone una sola URL (`http://localhost:7500`) que enruta a los 4
servicios, a Keycloak (`/auth/*`) y a los clientes web/móvil. **No** es la única frontera de
confianza: cada servicio revalida el JWT y aplica su propia autorización. Además sirve un
**hub de desarrollo** en `/` que centraliza los recursos del proyecto (Swagger por servicio,
Keycloak admin, RabbitMQ management, Aspire, y el **reporte de cobertura en `/coverage/`**) —
ver [`src/apps/edge-proxy/README.md`](../src/apps/edge-proxy/README.md).

---

## 3. Modelo del dominio

El lenguaje ubicuo vive en los `CONTEXT.md` de cada servicio (sección `## Language`, con lista
`_Avoid_:` de anti-sinónimos) y en el [glosario de producto](product/glossary.md). Vocabulario
de frontend: **etapa / misión / sesión** (nunca "sección" ni "play").

| Contexto | Agregado raíz | Entidades / VOs clave | Eventos de dominio |
|---|---|---|---|
| mission-management | `Mission` | `Section`, `Challenge`, `Play`, `Question`, `Search`, `Choice`, `Hint`, `PathItem` · VO `Difficulty`, `MissionGameType` | — |
| session-management | `LiveSession` | `SessionTeam`, `SessionTeamProgress`, `EvidenceSubmission`, `ReleasedHint` · VO `JoinCode`, `EnrollmentWindow`, `LiveSessionState`, `ValidationOutcome` | `EvidenceSubmitted`, `EvidenceValidated`, `ValidationOutcomeOverridden`, `HintReleased` |
| scoring-monitoring | `Scoreboard`, `SessionEventLog` | `TeamScore`, `ScoreEntry`, `Penalty` · servicio `RankingComparer` | — (consume auditoría) |
| user-management | — (fachada sobre Keycloak) | DTOs `OperatorDto`, `ParticipantDto` | — |

Notas de diseño honestas:
- Solo `session-management` modela **eventos de dominio + `AggregateRoot`** con despacho
  transaccional (`DomainEventsDispatchInterceptor`). Es deliberado: es el contexto con el flujo
  de negocio que debe auditarse.
- `user-management` **no tiene capa `Domain`** por decisión ([CONTEXT.md](../src/services/user-management/CONTEXT.md)):
  es un adaptador delgado sobre Keycloak, no un dominio con reglas propias.
- `Difficulty` está hoy como clase de constantes, no como VO instanciable — candidato menor a pulir.

Referencias: [ADR-002](architecture/adr/ADR-002-user-vs-session-team.md) (User ≠ Session Team),
[ADR-003](architecture/adr/ADR-003-livesession-as-session-management-aggregate.md) (LiveSession como agregado principal),
[ADR-004](architecture/adr/ADR-004-scoreboard-source-of-truth.md) (Scoreboard como fuente de verdad; Ranking derivado),
[ADR-005](architecture/adr/ADR-005-per-team-progression.md) (progresión por equipo).

---

## 4. Patrones de diseño

El mapa completo y **honesto** (Nominal / Equivalente funcional / Formalizable) vive en
[`docs/architecture/patterns-map.md`](architecture/patterns-map.md). Resumen para la defensa:

| Patrón | Estado | Dónde |
|---|---|---|
| **Composite** | Nominal | `Mission` → `Section` (recursivo) → `Challenge`/`Play` en `mission-management/.../Domain` |
| **State** | Equivalente funcional | Ciclo `Scheduled→Active→Paused→Finalized/Canceled` en `LiveSession` + `LiveSessionState` |
| **Strategy** | Equivalente funcional | `Scoreboard.ScoreFor(difficulty)` — puntaje por dificultad |
| **Template Method** | Equivalente funcional | Esqueleto compartido de `LiveSession.SubmitEvidence` / `SubmitTriviaAnswer` |
| **Chain of Responsibility** | Equivalente funcional | Pipeline MediatR: `UmbralLoggingBehavior` → `UmbralValidationBehavior` → handler |
| **Facade** | Equivalente funcional | `ParticipantsController` (auto-registro sobre Keycloak) |
| **Proxy** | Equivalente funcional | Remote proxies tras interfaz: `ScoringAuditHttpClient`, `KeycloakAdminApiClient` |

Patrones tácticos/arquitectónicos adicionales (bonus): **Ports & Adapters** (dominante),
**Aggregate + Repository**, **Transactional Outbox** ([ADR-014](architecture/adr/ADR-014-masstransit-transactional-outbox-for-audit.md)),
**Domain Events**, **Decorator** (`AuthHeaderForwardingHandler`, un `DelegatingHandler`),
**Idempotent Consumer** (dedupe por `EventId`).

> Los candidatos **Formalizable** (Strategy, Proxy de protección de pistas) están planificados
> como refactors de profundización de bajo riesgo; ver [§14](#14-estado-y-pendientes).

---

## 5. Principios SOLID

Anclas concretas (una clase real por principio):

- **SRP** — `Umbral.ServiceDefaults/UmbralLoggingBehavior.cs`: única responsabilidad, medir y
  loggear el resultado de un request; excluye explícitamente auditoría/logging de negocio.
- **OCP** — `ScoringMonitoring.Domain/Services/RankingComparer.cs` (`IComparer<RankingEntry>`):
  nuevas reglas de desempate se agregan por composición sin tocar consumidores.
- **LSP** — `ILiveSessionRepository : IRepository<LiveSession>`: la implementación real, el fake
  de tests y cualquier `IRepository<LiveSession>` son intercambiables.
- **ISP** — puertos angostos por rol: `ISessionRealtimeNotifier`, `IScoringAuditClient`,
  `IEmailNotificationService`, en lugar de un gateway gordo. Stores por agregado ([ADR-017](architecture/adr/ADR-017-aggregate-stores-as-contract-repositories.md)).
- **DIP** — `IOperatorAdministrationPort` (Application) implementado por `KeycloakAdminApiClient`
  (Infrastructure), inyectado vía `AddHttpClient<IOperatorAdministrationPort, KeycloakAdminApiClient>()`.

---

## 6. Cross-cutting concerns

Centralizados en `src/shared/Umbral.ServiceDefaults`:

- **Logging** — `UmbralLoggingBehavior` (pipeline MediatR): tiempo + resultado por request,
  `Warning` para `UmbralServiceException` categorizada, `Error` para no controladas; vía OpenTelemetry.
- **Middleware de excepciones** — `UmbralExceptionHandler` (`IExceptionHandler`): mapea
  `UmbralFailureCategory` → status HTTP + `ProblemDetails` (RFC 7807) con `code`/`category`.
- **Validación** — `UmbralValidationBehavior`: ejecuta validadores FluentValidation antes del
  handler y falla rápido (`CascadeMode.Stop`) lanzando `UmbralDomainException(Validation)`.
- **Seguridad por roles** — `KeycloakRoleClaimsTransformation` aplana los claims de realm/resource;
  `[Authorize(Roles = …)]` en los controllers de todos los servicios. Matriz completa en
  [`docs/architecture/authorization-matrix.md`](architecture/authorization-matrix.md).
- **Telemetría** — `AddUmbralTelemetry` (OTLP logs/métricas/trazas → Aspire dashboard).

Todo esto está cubierto por tests en `Umbral.ServiceDefaults.UnitTests`.

---

## 7. Pruebas y cobertura

**Frameworks:** xUnit + FluentAssertions (backend); Moq para mocks; Vitest (web) y Jest (mobile).

| Nivel | Dónde | Notas |
|---|---|---|
| Unitarias | `tests/*.UnitTests` por servicio + `Umbral.ServiceDefaults.UnitTests` | Reglas de dominio, handlers, behaviors |
| Integración (HTTP en proceso) | `tests/*.IntegrationTests` | `WebApplicationFactory` + EF InMemory; auth con `TestAuthenticationHandler` |
| Integración (BD real) | `MissionManagement.IntegrationTests` | **Testcontainers Postgres 16**; auto-skip si no hay Docker |
| E2E de caja negra | `scripts/Invoke-ComposeSmokeValidation.ps1` | Levanta el stack real y valida `/health` + OIDC + auth (`verify_auth.py`) |
| E2E realtime + mensajería | rama `feature/e2e-signalr-rabbitmq` | Hub SignalR real + flujo RabbitMQ con Testcontainers (en incorporación) |

**Mocks/stubs/fakes:** Moq en unit tests; fakes a mano (`FakeRepository`, `StubHttpMessageHandler`).
El notificador SignalR se prueba con `IHubContext` mockeado.

**Cobertura (coverlet + ReportGenerator):** último reporte agregado — **línea 66.9 %**,
**rama 69.8 %** (10 assemblies, 223 clases). El reporte HTML se sirve por el edge en `/coverage/`.

- Métrica gateada: **cobertura de rama** (no línea) — `scripts/Get-BackendCoverageSummary.ps1`.
- Umbral **enforzado** hoy: 10 % (gate temporal en `Invoke-RepositoryValidation.ps1`).
  **Meta académica: 90 % de rama** — reportada pero aún no enforzada. Ver [§14](#14-estado-y-pendientes).

---

## 8. Mensajería RabbitMQ

Flujo de negocio: **auditoría de la sesión en vivo** (el seam asíncrono exigido). Diseño en
[ADR-014](architecture/adr/ADR-014-masstransit-transactional-outbox-for-audit.md).

```
session-management                                   scoring-monitoring
──────────────────                                   ──────────────────
LiveSession.SubmitEvidence / ReleaseHint / …
  → levanta evento de dominio
DomainEventsDispatchInterceptor (SaveChanges)
  → SessionAuditEventMapper → SessionAuditEventMessage
  → IPublishEndpoint (EF Core outbox, misma transacción)   ⟶  RabbitMQ  ⟶  SessionAuditEventConsumer
                                                                              → LogSessionEventCommand
                                                                              → persiste SessionEventLog
                                                                              → push SignalR (ranking/monitor)
```

Claves: publicación **atómica** con la escritura de negocio (outbox → sin pérdida), entrega
**at-least-once**, **idempotencia por `EventId`** en el consumidor (sin inbox). El contrato
`SessionAuditEventMessage` vive en el namespace `Umbral.Contracts.Audit` y se duplica por
servicio a propósito ([ADR-012](architecture/adr/ADR-012-service-internal-layering-and-shared-kernel-placement.md)).
El puntaje/crédito primario sigue siendo HTTP síncrono ([ADR-011](architecture/adr/ADR-011-hybrid-service-communication.md)).

---

## 9. Tiempo real (SignalR)

`session-management` expone un hub SignalR (ruta `/session-hub/*` por el edge). El puerto
`ISessionRealtimeNotifier` (Application) lo implementa `SignalRLiveSessionRealtimeNotifier`
(Infrastructure). El token JWT viaja por query string `access_token` solo para el hub, patrón
estándar para WebSocket autenticado ([ADR-008](architecture/adr/ADR-008-signalr-for-real-time.md)).
La consola web y el móvil consumen el hub con `@microsoft/signalr` para reflejar en vivo
liberación de pistas, envío de evidencias y cambios de ranking.

---

## 10. Integración continua

Pipeline `.github/workflows/validation.yml` (detalle en [validation-pipeline](architecture/validation-pipeline.md)):

1. **backend-unit-tests** — lane rápida, solo unitarias, sin Docker.
2. **code-validation** — build Release + unit + integration + **cobertura** (coverlet →
   ReportGenerator) + lint/typecheck/build de web y mobile; publica `temp/validation` como artefacto.
3. **compose-smoke** — `docker compose up` del stack real + smoke de auth end-to-end.

Como primer paso de cada corrida se ejecuta `Test-VersionedSecrets.ps1` (escaneo de secretos por
regex). Etapas: restore → build → test → coverage → report → smoke.

---

## 11. Ejecución local

Guía completa y puertos en [local-infrastructure](architecture/local-infrastructure.md).

```bash
cp .env.example .env
docker compose -f docker-compose.dev.yml up -d      # o scripts/Start-Dev.ps1
```

Puntos de entrada:
- **Hub de desarrollo:** http://localhost:7500/  (enlaza todo lo de abajo)
- Web `:3000` · Mobile web `:19006` · Edge `:7500`
- Keycloak admin: http://localhost:7500/auth/admin/
- RabbitMQ management: http://localhost:16672
- Aspire dashboard: http://localhost:19888
- Cobertura (tras generarla): http://localhost:7500/coverage/

Generar el reporte de cobertura que consume el hub:

```bash
pwsh scripts/Invoke-RepositoryValidation.ps1     # corre tests + coverage + report
```

La BD es desechable (pre-release), las migraciones se aplican al arrancar
(`Persistence__ApplyMigrationsOnStartup: true`) — no hay paso manual de BD.

---

## 12. Refactorizaciones

Registro de mejoras estructurales aplicadas durante el semestre:

- **Split del kernel** — `Umbral.Kernel` (sin framework) separado de `Umbral.ServiceDefaults`
  ([ADR-015](architecture/adr/ADR-015-framework-free-shared-kernel.md), [PR-1 plan](refactoring/pr1-kernel-split-plan.md)).
- **Colapso de la trío de persistencia** — seam `IMissionStore`, se eliminaron
  `IUnitOfWork`/`IRepository` genéricos donde no aportaban ([ADR-017](architecture/adr/ADR-017-aggregate-stores-as-contract-repositories.md),
  [handoff C1](refactoring/handoff-c1-persistence.md), [plan C1-C2](refactoring/plan-c1-c2-kernel-y-persistencia.md)).
- **Auditoría EDD: de raw-client a outbox** — [ADR-013](architecture/adr/ADR-013-audit-domain-events-without-outbox.md)
  (aceptado) fue **superado** por [ADR-014](architecture/adr/ADR-014-masstransit-transactional-outbox-for-audit.md)
  (MassTransit + EF outbox). El [handoff original](handoff-rabbitmq-audit-edd.md) describe el enfoque anterior.
- **Reorganización DDD del dominio** por tipo (Aggregates/Entities/VOs/Events) manteniendo namespaces por agregado.
- **Refactor de la consola web** (flow board + alineación a paleta mobile) — [seguimiento](web-refactor-followups.md).

---

## 13. Trazabilidad rúbrica → evidencia

Tabla de defensa: cada tema del enunciado (§13) contra su evidencia concreta.

| Tema del enunciado | Evidencia en UMBRAL | Estado |
|---|---|---|
| Repo ordenado, robustez, excepciones | Estructura por contexto, jerarquía `UmbralServiceException`, `AGENTS.md` | ✅ |
| Arquitectura limpia / hexagonal | Capas + `ArchitectureInvariantsTests` (invariante verificada) | ✅ |
| DDD (agregados, repos, servicios, BC, lenguaje ubicuo) | §3 + `CONTEXT.md` por servicio + glosario | ✅ |
| Value Objects + IoC/DI | `JoinCode`, `EnrollmentWindow`, … + composition roots por servicio | ✅ |
| Pruebas unitarias | `tests/*.UnitTests` (xUnit) | ✅ |
| Mocks, stubs, fakes | Moq + fakes a mano | ✅ |
| Cobertura | coverlet + ReportGenerator; 66.9 % línea / 69.8 % rama; servido en `/coverage/` | ⚠️ meta 90 % rama no alcanzada/enforzada |
| SOLID | §5 (ancla concreta por principio) | ✅ |
| Cross-cutting | logging, excepciones, validación, seguridad por roles | ✅ |
| Strategy | `Scoreboard.ScoreFor` (equivalente funcional) | ⚠️ formalizable |
| Composite | `Mission`/`Section`/`Challenge` | ✅ nominal |
| Facade | `ParticipantsController` sobre Keycloak | ⚠️ equivalente funcional |
| Proxy | remote proxies tras interfaz | ⚠️ equivalente funcional |
| Template Method | `SubmitEvidence`/`SubmitTriviaAnswer` | ⚠️ equivalente funcional |
| State | ciclo de vida de `LiveSession` | ⚠️ equivalente funcional |
| Chain of Responsibility | pipeline MediatR (logging→validación→handler) | ⚠️ equivalente funcional |
| Integración continua | `validation.yml` (3 jobs) | ✅ |
| Docker / Docker Compose | `docker-compose.dev.yml` levanta todo el stack | ✅ |
| Pruebas de integración y E2E | Testcontainers (mission) + compose-smoke; SignalR/RabbitMQ E2E en incorporación | ⚠️ en refuerzo |
| Refactorización | §12 + `docs/refactoring/` + ADRs superados | ✅ |
| CQRS con MediatR | commands/queries + handlers + behaviors | ✅ |
| WebSockets observables | hub SignalR de sesión | ✅ |
| RabbitMQ en flujo de negocio | auditoría de sesión con outbox | ✅ |

---

## 14. Estado y pendientes

Honestidad técnica para la defensa (no ocultar deuda demuestra criterio):

1. **Cobertura vs meta** — el gate real es 10 % de rama; la meta académica de **90 % de rama**
   aún no se alcanza (69.8 %) ni se enforza. Además, proyectos vacíos reportan 100 % e inflan el
   agregado. Pendiente: subir cobertura y alinear el gate.
2. **Patrones formalizables** — Strategy (hoy `switch`) y un Proxy de protección real para pistas
   son refactors de bajo riesgo que convertirían "equivalente funcional" en "nominal".
3. **E2E de tiempo real y mensajería** — se están incorporando en `feature/e2e-signalr-rabbitmq`
   (hub SignalR con cliente real + flujo RabbitMQ con Testcontainers), hoy solo cubiertos por
   mocks + compose manual.
4. **Integración con BD real** — solo `mission-management` usa Testcontainers Postgres; los otros
   tres usan EF InMemory (no atrapan errores de SQL/FK/migración).
5. **Fuga suave de capa** — la capa `Application` depende transitivamente de ASP.NET/EF vía
   `Umbral.ServiceDefaults`. El dominio sí está aislado (verificado por test).
6. **Diagramas** — existe un único diagrama mermaid en un doc marcado como parcialmente superado;
   conviene un C4 de contenedores y una secuencia actualizada del flujo RabbitMQ/SignalR.
