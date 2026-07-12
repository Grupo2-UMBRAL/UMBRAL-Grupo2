# ADR-017: Stores por agregado como forma de los "repositorios definidos por contratos"

## Status

Accepted (complementa a [ADR-006](ADR-006-cqrs-single-database.md); reemplaza el uso de repositorios genéricos)

## Context

Los lineamientos técnicos del enunciado piden *"persistencia sobre PostgreSQL usando Entity Framework Core
y **repositorios definidos por contratos**"*. La implementación inicial cumplió esto con un repositorio
**genérico** por servicio: `IRepository<T> : IQueryable<T>` + `IUnitOfWork` + `I<Service>DbContext`,
implementados por un `Repository<T>` y el `DbContext`.

Ese trío tiene dos problemas de diseño:

1. `IRepository<T>` expone `IQueryable<T>`, así que **filtra EF Core y LINQ hacia la capa `Application`**:
   los handlers de comando arman `.Include(...)` y consultas sobre el repositorio. El "contrato" es en
   realidad toda la superficie de `IQueryable`, no un contrato de negocio acotado.
2. Un repositorio genérico no dice nada sobre las intenciones del agregado; es infraestructura disfrazada
   de dominio. La consistencia debería entrar por la raíz del agregado, no por un `IQueryable` abierto.

El refactor **C1** (`docs/refactoring/plan-c1-c2-kernel-y-persistencia.md`) reemplaza ese trío por
**stores de propósito, uno por agregado**. Este ADR registra la decisión y la reconcilia con el enunciado.

## Decision

### Lado comando — un store por agregado

Cada agregado expone un **store de escritura** con métodos de intención de negocio, no un `IQueryable`:

- Interfaz en `*.Application/Abstractions` (p. ej. `IMissionStore`).
- Adapter en `*.Infrastructure/Persistence` (p. ej. `MissionStore`, `internal sealed`), apoyado en el
  `DbContext`.
- Métodos con nombre de dominio: `GetAsync`, `GetWithItemsAsync`, `Add`, `ReplaceItemsAsync`,
  `NameExistsAsync`, `SaveChangesAsync`. Ninguno devuelve `IQueryable`.

**Estos stores SON los "repositorios definidos por contratos" que pide el enunciado**: son contratos
(interfaces en `Application`) con implementación intercambiable en `Infrastructure`, y los tests los
sustituyen por fakes en memoria (p. ej. `InMemoryMissionStore`, que cuenta `SaveChanges`). Cambia el
nombre ("Store" en vez de "Repository") y el hecho de que son específicos por agregado en lugar de
`IRepository<T>` genérico; la propiedad exigida — persistencia detrás de un contrato — se mantiene.

### Lado consulta — directo al `DbContext`

Coherente con [ADR-006](ADR-006-cqrs-single-database.md) (CQRS lógico): las **consultas no pasan por el
store**. Van directo al `DbContext` vía `I<Service>DbContext` con `AsNoTracking()` y proyección a DTOs de
lectura. Escritura y lectura son caminos distintos; el store es solo del lado escritura.

## Consequences

Positivas:

- Los handlers de comando quedan **100% mockeables sin EF Core**: un fake por agregado basta.
- Desaparece el `IRepository<T> : IQueryable<T>` que filtraba EF/LINQ a `Application` en el lado escritura.
- La superficie del contrato refleja las operaciones reales del agregado, no un CRUD genérico.

Coste / notas:

- El nombre literal "Repository" ya no aparece en el código de escritura. Se documenta el mapeo
  ("Store por agregado" = "repositorio por contrato" del enunciado) en `patterns-map.md` y aquí, para que
  la rúbrica y la defensa lo encuentren.
- La lectura usa EF Core directamente en `Application`; es una desviación consciente de clean architecture
  purista a favor de simplicidad de CQRS (ver ADR-006).

## Estado de ejecución (a 2026-07-10)

- **mission-management: migrado.** `IMissionStore` + `MissionStore`; `IUnitOfWork`/`IRepository` eliminados
  (PR-2, mergeado a `develop`).
- **session-management y scoring-monitoring: pendientes / mixtos.** `session` aún usa
  `IRepository<T>` + `IUnitOfWork`; `scoring` mezcla `IRepository<T>` con un store específico
  (`IApplyPenaltyScoreboardStore`). Homogeneizar a `ILiveSessionStore` / `IScoreboardStore` es el
  follow-up de C1 (PRs restantes).

## Guardrails

- Un store de escritura nunca expone `IQueryable<T>` en su contrato.
- El lado consulta nunca escribe; el store nunca se usa para leer proyecciones de query.

## Related

- Complementa [ADR-006](ADR-006-cqrs-single-database.md) (CQRS lógico, una BD).
- Detalle de ejecución: `docs/refactoring/plan-c1-c2-kernel-y-persistencia.md`,
  `docs/refactoring/handoff-c1-persistence.md`.
