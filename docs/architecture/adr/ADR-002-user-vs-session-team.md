# ADR-002: Separar User de Session Team

## Status

Accepted

## Context

El proyecto necesita distinguir entre identidad autenticada y participación dentro de una sesión. Un mismo usuario autenticado no es el mismo concepto que el equipo que compite en una sesión específica.

Si ambos conceptos se mezclan, se contamina la lógica de acceso con la lógica de progreso, puntaje y operación en vivo.

## Decision

- `User` pertenece a `Identity and Access`.
- `Session Team` pertenece a `Session Operations`.
- La identidad autenticada autoriza acciones.
- El progreso de juego, puntaje y estado competitivo pertenecen al equipo de sesión, no al usuario como identidad global.

## Consequences

- Los tokens y claims no sustituyen el modelo operativo del equipo.
- El backend debe modelar por separado autenticación/autorización y participación en la sesión.
- La documentación y los agentes no deben usar `User`, `Participant` y `Session Team` como sinónimos.
