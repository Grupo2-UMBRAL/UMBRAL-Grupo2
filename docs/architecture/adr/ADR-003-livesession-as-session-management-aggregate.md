# ADR-003: LiveSession es el agregado principal de Session Operations

## Status

Accepted

## Context

La operación en vivo requiere coordinar estado global de la sesión, equipos, enrolamiento, avance, flujo efectivo de etapas y aceptación operativa de evidencias.

Si estas reglas se distribuyen sin un agregado principal, se vuelven difíciles de proteger las invariantes de sesión.

## Decision

`LiveSession` es el agregado principal de `Session Operations`.

La consistencia operativa de:

- estado de sesión
- equipos de sesión
- enrolamiento
- flujo efectivo de etapas
- progreso por equipo
- aceptación operativa de evidencias

entra por `LiveSession`.

## Consequences

- Las reglas de transición y coordinación operativa se centralizan.
- Otros conceptos de `Session Operations` no deben comportarse como fuentes de verdad competidoras.
- La implementación puede descomponerse internamente, pero sin romper la autoridad del agregado sobre sus invariantes.
