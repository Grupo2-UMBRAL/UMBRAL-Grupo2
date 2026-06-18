# ADR-001: Separar UMBRAL en cuatro bounded contexts

## Status

Accepted

## Context

UMBRAL combina diseÃ±o reusable de misiones, operaciÃ³n en vivo, puntaje auditable e identidad. Tratar todo eso como un Ãºnico modelo hace mÃ¡s fÃ¡cil mezclar lenguaje, reglas e invariantes distintas.

El enunciado acadÃ©mico pide al menos tres bounded contexts o subÃ¡reas con lenguaje propio. El modelo actual ya distingue cuatro Ã¡reas con responsabilidades diferentes.

## Decision

La soluciÃ³n se modela con estos cuatro bounded contexts:

- `Mission Design`
- `Session Operations`
- `Scoring and Monitoring`
- `Identity and Access`

Las relaciones entre ellos se documentan en `CONTEXT-MAP.md`.

## Consequences

- El lenguaje del dominio queda particionado por contexto.
- Las decisiones de implementaciÃ³n deben respetar estos lÃ­mites antes de definir mÃ³dulos o despliegue.
- `CONTEXT.md` por contexto pasa a ser la referencia de vocabulario.
- Cualquier propuesta que cruce conceptos entre contextos debe justificarse explÃ­citamente.
