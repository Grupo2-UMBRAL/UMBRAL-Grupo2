# Mapa de patrones de diseño

Traza cada patrón que el enunciado espera evidenciar hasta la clase y el archivo reales que lo materializan,
más el test que lo cubre. La columna **Estado** es deliberadamente honesta:

- **Nominal** — implementado como el patrón GoF/táctico, reconocible por forma.
- **Equivalente funcional** — el comportamiento existe, pero no con la forma canónica del patrón (p. ej. un
  `switch` en vez de una jerarquía Strategy). Defendible como "el patrón resuelto pragmáticamente".
- **Formalizable** — candidato a convertir en nominal si se quiere evidencia de libro; ver
  `docs/refactoring/` y la revisión de arquitectura.

Rutas relativas a la raíz del repo. Todo verificado sobre `develop` a 2026-07-10.

## Patrones exigidos por el enunciado

| Patrón | Dónde en UMBRAL | Clase / archivo | Estado | Test |
|---|---|---|---|---|
| **Composite** | Jerarquía de misión: path → sections → challenges → plays | `mission-management/MissionManagement.Domain/Missions/{PathItem,Section,Challenge,Play,Question,Search}.cs` | **Nominal** — `PathItem` abstracto (Component), `Section` (Composite), `Challenge` (Leaf); `Play`/`Question`/`Search` jerárquicos | `MissionDomainTests` (flatten/invariantes) |
| **State** | Ciclo de vida de `LiveSession` (`Scheduled→Active→Paused→Finalized/Canceled`, RB-09) | `session-management/SessionManagement.Domain/LiveSessions/{LiveSession.cs,LiveSessionStates.cs}` | **Equivalente funcional / Formalizable** — hoy constantes string + guard clauses `EnsureState(...)`; candidato a VO-autómata o clases-estado | `LiveSessionDomainTests` |
| **Strategy** | Cálculo de puntaje por dificultad/modo de juego | `scoring-monitoring/ScoringMonitoring.Domain/Scoreboards/Scoreboard.cs` (`ScoreFor`) | **Equivalente funcional / Formalizable** — hoy `switch` (Easy/Medium/Hard); dos modos de juego reales lo harían seam nominal | `ScoringDomainTests` |
| **Template Method** | Flujo base de envío de evidencia con variante por tipo de juego | `session-management/…/LiveSessions/LiveSession.cs` (`SubmitEvidence` / `SubmitTriviaAnswer` + helpers privados compartidos) | **Equivalente funcional / Formalizable** — pasos compartidos por composición, no por herencia con `virtual` | `EvidenceSubmissionQaTests` |
| **Chain of Responsibility** | Cadena de validaciones antes de aceptar un caso de uso | `shared/Umbral.ServiceDefaults/{UmbralLoggingBehavior,UmbralValidationBehavior}.cs` (pipeline `MediatR`) + guard clauses de dominio | **Equivalente funcional** — pipeline de behaviors encadenados logging→validación→handler; es infra genérica, no una CoR de reglas de negocio | `UmbralValidationBehaviorTests`, `UmbralLoggingBehaviorTests` |
| **Facade** | Auto-registro público de participante sobre el flujo Keycloak | `user-management/UserManagement.Api/Controllers/ParticipantsController.cs` (fachada declarada) | **Equivalente funcional** — fachada ligera sobre creación de usuario + rol en Keycloak | `KeycloakAdminApiClientTests` |
| **Proxy** | Acceso a servicios/recursos remotos detrás de un puerto | `session-management/…/Infrastructure/ScoringAuditHttpClient.cs`, `…/MissionManagementLiveSessionCatalog.cs`; `user-management/…/Infrastructure/Services/Identity/KeycloakAdminApiClient.cs` | **Equivalente funcional** — remote proxies tras interfaces (`IScoringMonitoringClient`, `IOperatorAdministrationPort`) | `ScoringMonitoringHttpClientApplyPenaltyTests`, `KeycloakAdminApiClientTests` |

## Patrones tácticos / arquitectónicos adicionales presentes (bonus para la defensa)

| Patrón | Dónde | Clase / archivo | Estado |
|---|---|---|---|
| **Ports & Adapters (hexagonal)** | Puertos en `Application`, adapters en `Infrastructure`/`Api` | `ISessionRealtimeNotifier` → `SignalRLiveSessionRealtimeNotifier`; `IOperatorAdministrationPort` → `KeycloakAdminApiClient`; `IEmailNotificationService` → `SmtpEmailNotificationService` | **Nominal** — patrón dominante del backend |
| **Aggregate + Repository (DDD)** | Persistencia por contrato del agregado | `IMissionStore` → `MissionStore` (ver [ADR-017](adr/ADR-017-aggregate-stores-as-contract-repositories.md)) | **Nominal** (mission); pendiente homogeneizar session/scoring |
| **Transactional Outbox** | Publicación fiable de eventos de auditoría | `session-management/…/Infrastructure` — `AddEntityFrameworkOutbox` + `DomainEventsDispatchInterceptor` (ver [ADR-014](adr/ADR-014-masstransit-transactional-outbox-for-audit.md)) | **Nominal** (`MassTransit` + EF outbox) |
| **Domain Events** | Eventos levantados por el agregado | `session-management/…/Domain/LiveSessions/LiveSessionDomainEvents.cs` (`EvidenceSubmitted`, `EvidenceValidated`, `HintReleased`, …) | **Nominal** |
| **Decorator** | Reenvío del JWT en el pipeline HTTP saliente | `session-management/…/Infrastructure/AuthHeaderForwardingHandler.cs` (`DelegatingHandler`) | **Nominal** |
| **Idempotent Consumer** | Consumo at-least-once sin inbox | `scoring-monitoring/…/Messaging/SessionAuditEventConsumer.cs` + dedupe por `EventId` en `LogSessionEventCommandHandler` | **Nominal** |

## SOLID — anclas concretas

- **SRP** — separación de capas por servicio; handlers de un solo caso de uso.
- **OCP** — el candidato Strategy de puntaje (arriba) lo evidencia si se formaliza; hoy los subtipos de
  `Play` (`Question`/`Search`) extienden sin modificar el agregado.
- **LSP** — jerarquía `PathItem`/`Play` sustituible en `Mission.Flatten()`.
- **ISP** — stores por agregado (contratos acotados) en vez de un `IRepository<T>` genérico ([ADR-017](adr/ADR-017-aggregate-stores-as-contract-repositories.md)).
- **DIP** — Ports & Adapters: `Application` depende de interfaces, no de EF/Keycloak/SMTP/SignalR.

## Notas

- Los tres candidatos **Formalizable** (State, Strategy, Template Method) están descritos como refactors de
  profundización en la revisión de arquitectura de 2026-07-10; convertirlos a nominal es opcional y de bajo
  riesgo, con evidencia de rúbrica como principal beneficio.
- Este archivo debe actualizarse cuando un patrón cambie de estado (p. ej. si se formaliza el State de
  `LiveSession`, mover su fila a **Nominal** y apuntar a la nueva clase).
