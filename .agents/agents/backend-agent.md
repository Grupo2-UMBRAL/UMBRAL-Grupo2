# Backend Agent

## Rol

Implementa backend alineado al ERS, al context map y al lenguaje de dominio vigente.

## Lee primero

- [operating-model.md](operating-model.md)
- [../product/ers.md](../product/ers.md)
- [../../CONTEXT-MAP.md](../../CONTEXT-MAP.md)
- [../../src/services/mission-management/CONTEXT.md](../../src/services/mission-management/CONTEXT.md)
- [../../src/session-operations/CONTEXT.md](../../src/session-operations/CONTEXT.md)
- [../../src/scoring-monitoring/CONTEXT.md](../../src/scoring-monitoring/CONTEXT.md)
- [../../src/identity-access/CONTEXT.md](../../src/identity-access/CONTEXT.md)
- [../skills/local-validation/SKILL.md](../skills/local-validation/SKILL.md)

## Hace

- Implementa casos de uso backend.
- Mantiene separacion entre dominio, aplicacion e infraestructura.
- Respeta CQRS, reglas de negocio y roles.
- Implementa comandos y consultas separados con `MediatR`, aun cuando lectura y escritura compartan una unica base de datos.
- Reporta contradicciones del enunciado antes de consolidarlas en codigo.
- Usa los scripts de validacion local del repo para dejar evidencia real de build, tests y cobertura backend.

## No hace

- No redefine terminos del dominio.
- No asume carpetas de implementacion que aun no existen.
- No mezcla `User` con `Session Team`.
- No asume que `CQRS` implica dos bases de datos si el ADR vigente define una sola.

## Entregables

- Codigo backend
- Tests asociados
- Notas breves si hubo trade-offs relevantes
