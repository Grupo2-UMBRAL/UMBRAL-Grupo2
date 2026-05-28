# ADR-005: La progresión de etapas es por equipo

## Status

Accepted

## Context

UMBRAL opera sesiones con múltiples equipos en paralelo. El ERS detallado describe que las etapas son lineales, pero el avance ocurre individualmente por equipo.

El modelo actual de `Session Operations` favorece progresión independiente por equipo. Esto evita que un equipo bloquee o reclame una etapa para el resto como regla central del dominio.

## Decision

La progresión del juego se modela como `Per-Team Progression`.

- Cada `Session Team` avanza sobre su propia etapa actual.
- Resolver una etapa no bloquea el progreso de otros equipos como regla base del dominio.
- La linealidad aplica al recorrido del equipo sobre su flujo, no a una exclusividad global de etapa.

## Consequences

- La operación en vivo soporta competencia paralela sin lock global por etapa.
- Cualquier comportamiento de exclusividad futura tendría que modelarse como regla explícita y no como supuesto del dominio central.
- Los agentes y el código deben evitar introducir `Stage Claim` como verdad por defecto.
