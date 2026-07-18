# ADR-017: Repositorios por agregado y contratos explícitos de lectura

## Estado

Aceptado, revisado el 2026-07-17. Complementa a [ADR-006](ADR-006-cqrs-single-database.md) y reemplaza los repositorios genéricos.

## Contexto

El diseño inicial de persistencia usaba un repositorio genérico por servicio:
`IRepository<T> : IQueryable<T>`, `IUnitOfWork` e `I<Service>DbContext`.
Ese diseño filtraba EF Core y LINQ hacia Application, exponía una superficie de consulta sin
límites y ocultaba la intención del agregado detrás de CRUD genérico.

El proyecto exige persistencia sobre EF Core mediante repositorios definidos por contratos.
Un contrato solo sirve si expresa una necesidad acotada de Application y no expone la
tecnología de persistencia.

## Decisión

### Lado comando: un repositorio por agregado

Cada agregado expone un contrato de repositorio en `*.Application/Abstractions`, nombrado
por el agregado, por ejemplo `ILiveSessionRepository`. Infrastructure lo implementa como un
adaptador interno con el nombre correspondiente, por ejemplo `LiveSessionRepository`.

El contrato tiene métodos que revelan intención, como `GetForEnrollmentAsync`,
`GetForEvidenceSubmissionAsync`, `AddAsync` y `SaveChangesAsync`. Nunca expone
`IQueryable`, `DbSet`, expresiones para componer consultas ni tipos de EF Core.

Estos repositorios son los repositorios definidos por contratos exigidos por el proyecto:
Application es dueña de la interfaz e Infrastructure de la implementación con EF Core. Las
pruebas pueden sustituir el contrato por un fake en memoria.

### Lado consulta: contratos de lectura explícitos

Las consultas no usan el repositorio del agregado. Cada contexto expone un contrato de
lectura, como `ILiveSessionReadRepository` o `ILiveSessionQueries`, que devuelve proyecciones
materializadas o modelos de lectura necesarios para cada query.

Infrastructure implementa ese contrato con EF Core, `AsNoTracking()` y la estrategia de carga
requerida. Application no conoce `DbContext`, `DbSet`, `IQueryable` ni extensiones de EF.
Esto preserva el CQRS lógico de ADR-006 sin filtrar la persistencia a los handlers.

## Consecuencias

Positivas:

- Los handlers de comandos y consultas se prueban mediante seams explícitos, sin un proveedor de EF.
- Desaparecen `IRepository<T> : IQueryable<T>` y las fugas de EF Core en Application.
- Los contratos revelan operaciones del agregado o proyecciones de query, no CRUD genérico.

Costes:

- Los contratos de lectura agregan tipos y adaptadores.
- Los perfiles de carga de EF deben validarse en pruebas de integración de los adaptadores de Infrastructure.

## Estado de migración

- `mission-management`, `session-management` y `scoring-monitoring` deben migrar los repositorios genéricos a contratos específicos por agregado.
- Ningún contrato de Application puede exponer `DbContext` o `DbSet`.
- Los handlers de query migran de manera incremental a contratos de lectura explícitos.

## Guardrails

- Un repositorio de comandos nunca expone `IQueryable<T>`.
- Un contrato de lectura nunca escribe ni rehidrata un agregado para mutarlo.
- Ningún contrato de Application expone tipos de EF Core.

## Relacionados

- [ADR-006](ADR-006-cqrs-single-database.md)
