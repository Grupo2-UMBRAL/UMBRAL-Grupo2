# ADR-014: MassTransit con Outbox transaccional para eventos de auditoría

## Status

Accepted (supersede a [ADR-013](ADR-013-audit-domain-events-without-outbox.md))

## Context

`ADR-013` decidió publicar los eventos de auditoría (`SessionEventLog`) con `RabbitMQ.Client`
crudo, **después** del commit y en modo best-effort, **sin** outbox. Su argumento central era
que el outbox transaccional sería *plumbing caro* (tabla + relay `BackgroundService` a mano)
sin valor académico proporcional, y que la ventana de pérdida commit→publish era despreciable
a volumen de demo.

Dos cosas cambian ese cálculo:

1. Se adopta **MassTransit** (v8, Apache-2.0) como capa de mensajería en vez de
   `RabbitMQ.Client` crudo. MassTransit trae el **outbox transaccional EF Core de fábrica**
   (`AddEntityFrameworkOutbox` + `UseBusOutbox`): el "plumbing caro" que ADR-013 quería evitar
   deja de ser código propio a mantener y pasa a ser configuración.
2. Con el outbox ya gratis, la única razón para conservar el best-effort desaparece. La
   auditoría es precisamente el flujo donde perder un evento en silencio es indeseable.

## Decision

- **Transporte:** MassTransit sobre RabbitMQ reemplaza el `RabbitMQ.Client` crudo en ambos
  lados (publisher en `session-management`, consumer en `scoring-monitoring`).
- **Productor:** los eventos de dominio del agregado `LiveSession` se publican vía
  `IPublishEndpoint` **dentro** de la transacción de negocio (interceptor de `SavingChanges`,
  no `SavedChanges`), y quedan capturados por el **outbox EF Core** como filas `OutboxMessage`
  en el mismo `SaveChanges`. Un delivery service en segundo plano los relaya a RabbitMQ. La
  escritura de negocio y el evento de auditoría son **atómicos**.
- **Consumidor:** un `IConsumer<SessionAuditEventMessage>` de MassTransit reemplaza el
  `BackgroundService` hecho a mano. MassTransit gestiona suscripción, `ack`/`nack` y reintentos
  (5 × 5 s) antes de dead-letter.
- **Idempotencia:** se mantiene la deduplicación por `EventId` en `LogSessionEventHandler`
  (entrega at-least-once). No se usa el inbox de MassTransit porque la dedup ya vive en el
  handler.
- **Contrato:** `SessionAuditEventMessage` se mantiene **duplicado por contexto** (ADR-012),
  pero ambas copias comparten el namespace `Umbral.Contracts.Audit` para que el URN de mensaje
  de MassTransit (`urn:message:{namespace}:{tipo}`) coincida entre publisher y consumer.
- **Gate de tests:** `Messaging:UseRabbitMq=false` conmuta al transporte in-memory sin outbox,
  para que los tests de integración corran sin broker ni Postgres.
- **Health:** `/health` queda como liveness y **excluye** el health check del bus (tag
  `masstransit`), que reporta Unhealthy hasta que la conexión al broker está lista.

## Consequences

- La entrega de auditoría pasa de **at-most-once best-effort** a **at-least-once fiable**: si
  el broker está caído, los eventos quedan en `OutboxMessage` y se entregan cuando vuelve; ya
  no se pierden en la ventana commit→publish.
- Aparecen tablas de infraestructura en el schema `session_management`
  (`OutboxState`/`OutboxMessage`/`InboxState`, migración `AddMassTransitOutbox`). La BD es
  descartable pre-release, así que el costo de la migración es nulo.
- Se elimina el guardrail de ADR-013 de "despachar **después** del commit": con el outbox el
  despacho ocurre **dentro** de la transacción a propósito — es lo que da la atomicidad, y ya
  no hay riesgo de eventos fantasma porque la fila de outbox hace rollback con el negocio.
- Las variables de entorno (`RabbitMQ__Host/Port/User/Password`) y el `docker-compose` no
  cambian: MassTransit lee la misma sección `RabbitMQ` de configuración.

## Guardrails

- El evento de dominio lleva **datos estructurados**, no strings de presentación; el mapeo a
  texto vive en Infrastructure (`SessionAuditEventMapper`). (Se conserva de ADR-013.)
- Mantener el consumer **idempotente** (dedup por `EventId`): la entrega es at-least-once.
- Ambas copias del contrato deben conservar el **mismo namespace** `Umbral.Contracts.Audit`, o
  el ruteo por URN de MassTransit deja de coincidir. No mover el contrato a `src/shared`
  (ADR-012).
- Usar **MassTransit v8** (Apache-2.0). v9+ pasó a licencia comercial.

## Related decisions

- `ADR-013`: decisión previa (best-effort sin outbox), superseded por esta.
- `ADR-011`: comunicación híbrida; el flujo principal (score) sigue síncrono, solo lo
  secundario (auditoría) va async.
- `ADR-012`: `src/shared` no aloja contratos de negocio; los contratos de eventos se duplican
  por contexto.
- `ADR-008`: `SignalR` para tiempo real, canal distinto de `RabbitMQ`.
