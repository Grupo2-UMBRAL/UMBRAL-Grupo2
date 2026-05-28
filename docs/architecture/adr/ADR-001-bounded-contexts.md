# ADR-001: Separar UMBRAL en cuatro bounded contexts

## Status

Accepted

## Context

UMBRAL combina diseño reusable de misiones, operación en vivo, puntaje auditable e identidad. Tratar todo eso como un único modelo hace más fácil mezclar lenguaje, reglas e invariantes distintas.

El enunciado académico pide al menos tres bounded contexts o subáreas con lenguaje propio. El modelo actual ya distingue cuatro áreas con responsabilidades diferentes.

## Decision

La solución se modela con estos cuatro bounded contexts:

- `Mission Design`
- `Session Operations`
- `Scoring and Audit`
- `Identity and Access`

Las relaciones entre ellos se documentan en `CONTEXT-MAP.md`.

## Consequences

- El lenguaje del dominio queda particionado por contexto.
- Las decisiones de implementación deben respetar estos límites antes de definir módulos o despliegue.
- `CONTEXT.md` por contexto pasa a ser la referencia de vocabulario.
- Cualquier propuesta que cruce conceptos entre contextos debe justificarse explícitamente.
