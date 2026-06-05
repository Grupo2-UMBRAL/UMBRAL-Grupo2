# ADR-006: Aplicar CQRS lógico sobre una única base de datos

## Status

Accepted

## Context

UMBRAL necesita evidenciar separación entre comandos y consultas como parte del enfoque `CQRS` y del uso de `MediatR`. Al mismo tiempo, el proyecto no necesita asumir la complejidad operativa de mantener bases de datos separadas para lectura y escritura.

Separar modelos de lectura y escritura no obliga a separar almacenamiento físico. Para este proyecto, la complejidad adicional de dos bases de datos no está justificada por el alcance actual.

## Decision

- `CQRS` se implementa de forma lógica en la capa de aplicación.
- Los comandos y consultas se modelan por separado usando `MediatR`.
- Lecturas y escrituras pueden usar proyecciones y contratos distintos.
- Tanto comandos como consultas operan sobre una única base de datos relacional compartida.

## Consequences

- Se conserva la separación conceptual de `CQRS` sin introducir infraestructura adicional innecesaria.
- El proyecto sigue pudiendo demostrar comandos, consultas y modelos diferenciados.
- No existe desacoplamiento físico entre lectura y escritura.
- Si en el futuro aparecen necesidades reales de escalado o proyecciones independientes, esta decisión puede revisarse.
