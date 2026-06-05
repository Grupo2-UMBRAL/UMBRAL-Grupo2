# ADR-010: Adoptar microservicios reales desde el primer dia

## Status

Accepted

## Context

UMBRAL ya definio bounded contexts claros y necesita separar capacidades de dominio, tiempo real, scoring e identidad. El equipo decide que la arquitectura no arrancara como monolito modular con extraccion futura, sino como microservicios reales desde el inicio.

Esto impacta despliegue, contratos, mensajeria, persistencia, observabilidad y entorno local.

## Decision

- La solucion se implementa como microservicios reales desde el primer dia.
- Los servicios se alinean inicialmente con:
  - `Mission Design`
  - `Session Operations`
  - `Scoring and Audit`
  - `Identity and Access`
- `Identity and Access` es la capacidad logica; `Keycloak` es su implementacion inicial.
- `RabbitMQ` se usa para comunicacion asincrona entre servicios.

## Consequences

- El entorno local necesita varios servicios coordinados por `docker-compose`.
- Los contratos entre servicios se vuelven una preocupacion de primer orden.
- Las decisiones de auth, tiempo real y mensajeria deben pensarse de forma distribuida.
- La complejidad operacional sube desde el inicio, pero a cambio la arquitectura queda alineada con el objetivo real del sistema.
