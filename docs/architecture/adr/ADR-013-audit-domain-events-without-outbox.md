# ADR-013: Eventos de dominio de auditoría sin patrón Outbox

## Status

Accepted

## Context

Se introdujo comunicación event-driven para el flujo de **auditoría** (`SessionEventLog`): `session-management` emite eventos de dominio cuando ocurren hechos auditables (evidencia enviada/validada, pista liberada, override de validación) y los publica en `RabbitMQ`; `scoring-monitoring` los consume y persiste. Esto cumple `RF-14` (publicar eventos de dominio en `RabbitMQ`) y `RNF-05` (desacople asíncrono), en línea con `ADR-011` (comunicación híbrida: el flujo principal de score sigue síncrono por HTTP; solo lo secundario va async).

Queda una decisión de fiabilidad: al publicar **después** de confirmar la transacción de negocio, existe una ventana de doble escritura (dual-write) — si el proceso cae entre el commit y el publish, o si el broker está caído al publicar, el evento se pierde. El patrón **Outbox transaccional** (escribir el evento en la misma transacción que el cambio de negocio + un relay que publica desde la tabla) cierra esa ventana.

## Decision

Los eventos de auditoría se modelan como **eventos de dominio** del agregado `LiveSession` y se despachan a `RabbitMQ` **después del commit** (interceptor de `SavedChanges`), en modo **best-effort**, **sin** tabla de outbox ni relay.

Motivos:

1. El enunciado exige eventos de dominio en `RabbitMQ` y desacople async, **no** un outbox. El outbox es un detalle de fiabilidad, no un requisito.
2. La rúbrica premia arquitectura limpia / DDD táctico. Los eventos de dominio llenan un hueco real (los agregados no tenían ninguno); el outbox añadiría plumbing sin valor académico proporcional.
3. A volumen de demo, la probabilidad de caer en la ventana commit→publish es despreciable.

## Consequences

- Se acepta entrega **at-most-once** en el caso raro de fallo: si el broker está caído al publicar, o el proceso cae entre commit y publish, ese evento de auditoría se pierde silenciosamente (se loguea el error).
- El caso **consumer caído** sigue cubierto sin outbox: el broker retiene los mensajes en la cola hasta que el consumer vuelve (validado en pruebas).
- Los eventos de dominio quedan como base reutilizable. Si en el futuro se necesita fiabilidad garantizada, el outbox es un **add localizado** encima: escribir la fila de outbox en el interceptor (misma transacción) + un relay `BackgroundService`. **No re-litigar esto como un defecto de "falta outbox"** — es una omisión consciente.
- El flujo principal de score credit (síncrono por HTTP, `ADR-011`) es un problema de fiabilidad **aparte** y no lo cubre esta decisión.

## Guardrails

- Despachar los eventos **después** del commit (`SavedChanges`), **nunca** dentro de la transacción de negocio: publicar dentro arriesga eventos fantasma si el commit hace rollback.
- El evento de dominio lleva **datos estructurados**, no strings de presentación; el mapeo a texto/mensaje vive en Infrastructure.
- Mantener los consumers **idempotentes** (dedup por `EventId`): un `nack`/requeue puede redelivery aunque no haya outbox.
- No mover contratos de eventos a `src/shared` (`ADR-012`): cada contexto tiene su copia del contrato de la cola.

## Related decisions

- `ADR-011`: comunicación híbrida; el flujo principal (score) sigue síncrono, solo lo secundario (auditoría) va async.
- `ADR-008`: `SignalR` para tiempo real. `SignalR` y `RabbitMQ` son canales distintos: `SignalR` = última milla a la UI conectada; `RabbitMQ` = salto desacoplado entre servicios, previo al push de `SignalR`.
- `ADR-012`: `src/shared` no aloja contratos de negocio; los contratos de eventos se duplican por contexto.
