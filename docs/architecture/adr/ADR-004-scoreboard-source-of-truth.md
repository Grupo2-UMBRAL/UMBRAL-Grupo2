# ADR-004: Scoreboard es la fuente de verdad del puntaje y Ranking es derivado

## Status

Accepted

## Context

UMBRAL necesita recalcular puntaje, aplicar penalizaciones, explicar trazabilidad y mostrar ranking en tiempo real. Si el ranking se trata como fuente de verdad, se debilita la trazabilidad del puntaje acumulado.

El contexto de `Scoring and Monitoring` ya distingue entre cambios atÃ³micos de puntaje y la proyecciÃ³n observable del ranking.

## Decision

- `Scoreboard` es la fuente de verdad del puntaje acumulado por sesiÃ³n.
- `Score Entry` registra cambios atÃ³micos del puntaje.
- `Ranking` es una proyecciÃ³n derivada del `Scoreboard`.

## Consequences

- El ranking puede recalcularse sin redefinir la verdad del puntaje.
- La trazabilidad de puntaje y penalizaciones se mantiene separada de la vista observable.
- Los cambios de UI o tiempo real sobre ranking no deben redefinir el modelo de scoring.
