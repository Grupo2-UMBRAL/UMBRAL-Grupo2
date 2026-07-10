# Handoff — Event-Driven Audit (RabbitMQ) + Backend Architecture Review

Fecha: 2026-07-09 · Branch: `feature/rabbitmq-audit-events` (worktree en `.claude/worktrees/rabbitmq-audit`)
Estado: **Fase 1 implementada y validada end-to-end · sin commitear todavía · Fase 2 diseñada, no implementada.**

---

## 0. Cómo llegamos aquí

Se corrió `/improve-codebase-architecture` sobre el backend. Salió un **reporte HTML** (en temp) con 9 candidatos de deepening (C1–C9) + una matriz estructura-vs-ideal. Resumen del review:
- El árbol ya sigue el ideal (4 `.csproj`/servicio); los dominios (`Mission`, `LiveSession`, `Scoreboard`) son deep y bien testeados. La fricción está en el plumbing, no en el dominio.
- **Candidatos abiertos (NO tocados aún):** C1 colapsar trío de persistencia (`IRepository:IQueryable`+`IUnitOfWork`+`I*DbContext`), C2 partir el shared kernel (Domain arrastra EF/MediatR/ASP.NET), C3 reconectar pipeline MediatR (**bug real**: mission-management corre sin logging/validación — `Program.cs:9` llama `AddMediatR` pelado y nunca `AddMissionManagementApplication()`), C4 dedup handlers de evidencia, C5 Domain ceremonial de user-management, C6 `State` string→VO en session, C7 terminar rename Stage→Play, C9 adelgazar edge-proxy.
- **🔒 Seguridad pendiente (fuera de arquitectura):** Gmail app password real commiteada en `src/services/user-management/UserManagement.Api/appsettings.Development.json:13`. Rotar + sacar del repo.
- El usuario eligió **C8** (el seam async que el enunciado exige y no existía).

## 1. Decisiones de arquitectura (EDD)
- Transporte: **`RabbitMQ.Client` v7 crudo**, NO MassTransit (un solo flujo no justifica framework; hand-roll demuestra el patrón para la rúbrica).
- Alcance: **solo auditoría**. "Notificaciones"/"recálculo" son ejemplos opcionales del enunciado ("tales como"); SignalR ya cubre la UI en vivo; las notificaciones de login son auth (fuera).
- **SignalR ≠ RabbitMQ**: SignalR = última milla a la UI conectada (se queda en hot path); RabbitMQ = salto desacoplado entre servicios, va *antes* del push de SignalR.
- Split ADR-011: **score credit sigue síncrono por HTTP** (ranking inmediato, RF-12); solo auditoría va async.
- Contrato del evento **duplicado por servicio** (ADR-012: sin contratos de negocio en `src/shared`).

## 2. Fase 1 — DONE y validada

Flujo: `handler → ISessionAuditEventPublisher → RabbitMqPublisher → exchange umbral.events (key session.audit.logged) → queue scoring.audit.session-events → SessionAuditEventConsumer (BackgroundService) → ISender.Send(LogSessionEventCommand) → persiste SessionEventLog + push SignalR`.

**Archivos nuevos (9):** en cada servicio `Messaging/{RabbitMqOptions,RabbitMqConnection,UmbralAuditMessaging,SessionAuditEventMessage}.cs`; session `RabbitMqSessionAuditEventPublisher.cs` + `Application/Abstractions/Messaging/ISessionAuditEventPublisher.cs`; scoring `SessionAuditEventConsumer.cs`.
**Refactor de paso:** se extrajo `ISessionAuditEventPublisher`, sacando `LogSessionEventAsync` de `IScoringMonitoringClient`/`ScoringMonitoringHttpClient` (el HTTP client dejó de mezclar audit con score/penalty). 4 handlers (SubmitEvidence, SubmitTriviaAnswer, OverrideValidationOutcome, ReleaseHint) ahora publican vía el port.
**Paquete:** `RabbitMQ.Client 7.0.0` en `Directory.Packages.props` (CPM) + refs en ambos `.Infrastructure.csproj`.
**Config:** ya llega por compose (`RabbitMQ__Host/Port/User/Password` a session y scoring).

**Build/test:** producción compila; `SessionManagement.UnitTests` **158/158**; tests de auditoría reapuntados del HTTP viejo al publisher. Único build error restante = **pre-existente en develop**, ajeno a esto: `user-management/tests/.../CreateParticipantCommandHandlerTests.cs` (`participantUser` indefinido + falta `.Should()` de FluentAssertions).

**Validación end-to-end (con docker desde el worktree):** logs `Published audit event ...` (session) → `Persisted audit event ...` (scoring) calzando por GUID. Queue con `consumers=1`, `messages=0` (consumo instantáneo = sano). Experimento confirmado: al parar scoring, los mensajes se **acumulan** en la queue y session sigue operando; al reencender, se **drenan**. (Nota: eso prueba resiliencia a consumer-down con broker-up.)

**Observación importante del usuario:** al responder **correcto**, el sistema falló porque `RecordStageCreditAsync` es **HTTP síncrono** a scoring (que estaba caído). Eso es el flujo principal sync (ADR-011), NO lo arregla el outbox. Follow-up posible: retry/circuit-breaker en el HTTP de score, o reconsiderar. Fuera de alcance actual.

## 3. Fase 2 — DISEÑADA, no implementada

**Decisión: domain events SÍ, outbox NO** (decisión consciente del usuario, respaldada).
- El agregado `LiveSession` **no tiene ningún domain event** — hueco real (se construyó CQRS-first; los "hechos" se expresaron como llamadas imperativas). Fase 2 lo corrige.
- **Outbox deliberadamente omitido:** no lo pide el enunciado; la rúbrica quiere clean/DDD, no reliability plumbing; riesgo de la ventana de crash despreciable a volumen demo. Tradeoff aceptado: publish best-effort → si el **broker** está caído al publicar (o session crashea entre commit y publish) el evento se pierde (consumer-down sigue OK, el broker sostiene). **Regla firme:** dispatch POST-commit (`SavedChanges`), nunca dentro de la transacción (evita eventos fantasma en rollback).
- Domain events son la base; outbox sería un add localizado después. Ofrecido un ADR para registrar la omisión del outbox.

**Plan de implementación (Fase 2):**
- Domain (`SessionManagement.Domain`): `IDomainEvent` (marker `EventId`, `OccurredOnUtc`); base `AggregateRoot` con `DomainEvents`/`RaiseDomainEvent`/`ClearDomainEvents` (EF `Ignore`); `LiveSession` la hereda; ~4 eventos con datos estructurados (`EvidenceSubmitted/EvidenceValidated/HintReleased/ValidationOutcomeOverridden`); emitirlos en `SubmitEvidence/SubmitTriviaAnswer/ReleaseHint/OverrideValidationOutcome`.
- Infrastructure: `DomainEventsDispatchInterceptor : SaveChangesInterceptor` que en **SavedChanges** (post-commit) recoge los domain events de los agregados trackeados, los mapea a `SessionAuditEventMessage` (el template de la descripción vive aquí, no en el dominio), publica a RabbitMQ (reusa `RabbitMqConnection`/exchange de Fase 1) y limpia los eventos. Se retira `RabbitMqSessionAuditEventPublisher` de Fase 1.
- Handlers: se **revierten** las llamadas a `auditEventPublisher` (los eventos salen del agregado). Handlers más limpios que en Fase 1.
- scoring: idempotencia en el consumer (usar `EventId` como clave del `SessionEventLog` / índice único) por si hay redelivery en nack-requeue.
- Tests: los de auditoría pasan a verificar **domain events** (unit sobre el agregado) — assertion más fuerte que mock del handler.

## 4. Próximos pasos
1. Implementar Fase 2 (empezar por infra domain events + agregado, compilar, seguir con interceptor + idempotencia + tests). Todo en esta MISMA branch.
2. Probar end-to-end con docker desde el worktree.
3. Merge **no-ff** a `develop` cuando esté probado (regla del repo: `git-no-ff-merge`).
4. (Opcional) ADR registrando "outbox omitido a propósito".
5. Backlog separado: bug C3 (mission MediatR pipeline), secret de user-management, y el resto de C1–C9 del review.

## 5. Estado git
- Branch `feature/rabbitmq-audit-events` desde `develop@1ada555`, en worktree `.claude/worktrees/rabbitmq-audit`.
- **Fase 1 sin commitear.** El usuario quiere Fase 1 + Fase 2 juntas y UN solo no-ff al final.
- Correr el stack: `cd` al worktree → `docker compose -f docker-compose.dev.yml down` (baja el stack viejo, container_names colisionan) → `docker compose -f docker-compose.dev.yml up -d --build`.
