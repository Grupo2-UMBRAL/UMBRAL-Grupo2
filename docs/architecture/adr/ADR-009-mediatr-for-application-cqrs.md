# ADR-009: Usar MediatR para la capa de aplicación y CQRS lógico

## Status

Accepted

## Context

UMBRAL necesita evidenciar separación entre comandos y consultas dentro de la capa de aplicación. El proyecto también requiere una forma consistente de orquestar casos de uso sin acoplar controllers, handlers, validaciones y dependencias de infraestructura.

El proyecto ya decidió aplicar `CQRS` de forma lógica sobre una única base de datos compartida. Para sostener esa separación de manera uniforme, conviene usar un mecanismo explícito de mediación.

## Decision

- La capa de aplicación usará `MediatR`.
- Cada caso de uso se modelará como `Command` o `Query`.
- Los entry points despacharán solicitudes a `MediatR` en vez de ejecutar lógica de aplicación directamente.
- La separación `CQRS` será lógica: handlers distintos para lectura y escritura, aunque operen sobre la misma base de datos.

## Consequences

- Los controllers y adaptadores de entrada quedan delgados.
- La lógica de aplicación se organiza alrededor de casos de uso explícitos.
- Validaciones, autorizaciones de aplicación y comportamientos transversales pueden componerse alrededor del pipeline de `MediatR`.
- El proyecto puede demostrar `CQRS` sin requerir dos bases de datos ni dos infraestructuras de persistencia.
