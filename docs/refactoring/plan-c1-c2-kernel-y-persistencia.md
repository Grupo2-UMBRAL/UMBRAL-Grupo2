# Plan de acción — C2 (split del shared kernel) + C1 (colapso del trío de persistencia)

> Origen: revisión de arquitectura 2026-07-09 (`/improve-codebase-architecture`).
> C2 = Domain deja de arrastrar EF/MediatR/ASP.NET vía `Umbral.ServiceDefaults`.
> C1 = borrar `IUnitOfWork` + `IRepository<T> : IQueryable<T>` y dejar un solo seam de persistencia por servicio.

## Estado verificado (2026-07-10)

- `Umbral.ServiceDefaults.csproj` referencia: ASP.NET Core (FrameworkReference), MediatR, FluentValidation ×2, JwtBearer, EF Core, Npgsql, OpenTelemetry ×5.
- Los Domain de **mission, session y scoring** referencian `Umbral.ServiceDefaults` (user-management Domain no referencia nada). Lo consumen solo para `UmbralDomainException` / `UmbralTechnicalException` / `UmbralFailureCategory` (29 archivos de Domain).
- `IUnitOfWork`: 15 handlers (4 mission, 2 scoring, 9+ session). `IRepository<T>`: ~35 archivos.
- `IScoringMonitoringDbContext`: 0 consumidores reales (solo marker de assembly-scan + 1 registro DI).
- Branch en curso `feature/masstransit-audit-outbox` (ADR-014) toca `SessionManagement.Infrastructure` y `ScoringMonitoring.Infrastructure` → **prerrequisito: mergear ese branch antes de empezar**.

## Reglas del repo que aplican

- TDD y commits pequeños (AGENTS.md); merges `--no-ff`; build+test después de cada merge.
- ADR-012: shared = solo técnico cross-cutting. El split lo refuerza; la permanencia de `UmbralRoles` en shared se registra como excepción explícita (amendment).

---

## PR-1 · C2: partir el kernel — `Umbral.Kernel` sin framework

> Plan detallado y ejecutable: [pr1-kernel-split-plan.md](pr1-kernel-split-plan.md)

**Branch:** `refactor/kernel-split` (desde `develop`, post-merge del outbox).
**Idea clave:** los tipos movidos **conservan el namespace `Umbral.ServiceDefaults`** → cero ediciones de código fuente en los servicios; solo cambian `.csproj`. (Rename de namespace: opcional, PR aparte, mecánico.)

| Commit | Contenido | Verificación |
|---|---|---|
| 1 | Crear `src/shared/Umbral.Kernel/` (**sin** PackageReference alguno). Mover del ServiceDefaults: `UmbralServiceException`, `UmbralDomainException`, `UmbralTechnicalException`, `UmbralFailureCategory`, `UmbralRoles`. `Umbral.ServiceDefaults` referencia a `Umbral.Kernel` (los consumidores de ServiceDefaults no notan nada). Añadir ambos al `.sln`. | `dotnet build` solución completa |
| 2 | Los 3 `*.Domain.csproj` (mission, session, scoring): cambiar la referencia `Umbral.ServiceDefaults` → `Umbral.Kernel`. | `dotnet build` + tests unitarios de dominio de los 3 servicios |
| 3 | Test de arquitectura que fije el invariante: `Umbral.Kernel` no referencia paquetes; ningún `*.Domain` referencia `Umbral.ServiceDefaults`, EF, MediatR ni ASP.NET (basta un test que inspeccione `Assembly.GetReferencedAssemblies` o los `.csproj`). Ubicación: `src/shared/Umbral.Kernel.UnitTests/` o el proyecto de tests compartido. | test rojo→verde |
| 4 | ADR-015: "Split del shared kernel: `Umbral.Kernel` (sin framework) vs `Umbral.ServiceDefaults` (defaults web)". Registrar decisión sobre `UmbralRoles` (se queda en Kernel como lenguaje de autorización compartido — excepción consciente a ADR-012). Actualizar `docs/architecture/repo-structure.md` (que además sigue diciendo "un solo `.Api` por servicio" — desactualizado; corregir de paso). | revisión de docs |

**Fuera de alcance de PR-1 (registrar como follow-ups, no hacer):** colapsar la jerarquía de excepciones a un solo tipo concreto; renombrar namespace a `Umbral.Kernel`.

**Riesgo:** bajo. Movimiento estructural puro; el compilador ataja todo.

---

## PR-2 · C1 en mission-management (el caso más simple, valida el patrón)

**Branch:** `refactor/mission-persistence-seam`.

Decisión de diseño (aplica a los tres servicios):
- **Comandos** → interfaz de propósito por agregado (`IMissionStore`): `Task<Mission?> GetAsync(Guid, ct)`, `void Add(Mission)`, `Task SaveChangesAsync(ct)`. Implementación en Infrastructure (absorbe lo que hoy hace `MissionLoader` con sus 6 queries). Handlers de comando quedan 100% mockeables — EF desaparece de sus archivos.
- **Queries** → siguen leyendo del DbContext (CQRS, ADR-006): se quedan en `IMissionManagementDbContext`, aceptando EF en el lado de lectura. Es una postura defendible y evita reescribir proyecciones.
- `IUnitOfWork` e `IRepository<T>` se **borran**.

| Commit | Contenido |
|---|---|
| 1 | (TDD) Tests de `IMissionStore` fake + tests unitarios de los 4 command handlers contra el store (hoy 6 de 8 handlers no tienen unit test — este commit los crea en rojo). |
| 2 | Crear `IMissionStore` (Application/Abstractions) + `MissionStore` (Infrastructure/Persistence), absorbiendo `MissionLoader.RequireAsync`. Migrar los 4 command handlers (Create/Update/Activate/Deactivate). De paso: `Deactivate` deja de cargar el agregado completo (hoy hace 5 queries de más, copy-paste de Activate). Tests en verde. |
| 3 | Borrar `IUnitOfWork`, `IRepository<T>`, `Repository<T>` y `RepositoryTests`. Los 2 query handlers que usaban `IRepository` pasan a `IMissionManagementDbContext`. Ajustar `ServiceCollectionExtensions` (quitar registros muertos). |
| 4 | **Fix C3 aquí (mismo archivo):** `Program.cs:9` pasa a llamar `AddMissionManagementApplication()` (hoy el pipeline de logging/validación no corre — bug). Si se prefiere, mover este fix a su propio PR, pero toca la misma línea que el registro del store. |

**Verificación:** `dotnet test` unit + integration de mission (los integration tests con Testcontainers son la red de seguridad de conducta); smoke por docker compose.

---

## PR-3 · C1 en scoring-monitoring

**Branch:** `refactor/scoring-persistence-seam`. **Rebase sobre el outbox mergeado** (Infrastructure cambió en ese branch).

| Commit | Contenido |
|---|---|
| 1 | (TDD) Tests del `IScoreboardStore` unificado: load-or-create, save. Cubrir el caso duplicado-de-penalización que hoy vive en el `catch` del handler. |
| 2 | Renombrar/ampliar `IApplyPenaltyScoreboardStore` → `IScoreboardStore` y usarlo también en `RecordStageCreditHandler` (hoy duplica el load-or-create inline). Un solo seam para el agregado `Scoreboard`. |
| 3 | Borrar `IRepository<T>`/`Repository<T>`/`RepositoryTests` y `IUnitOfWork`. `LogSessionEventCommandHandler` y los 2 query handlers pasan al DbContext. |
| 4 | Borrar `IScoringMonitoringDbContext` **como abstracción**: sustituir los 2 markers de assembly-scan (`RegisterServicesFromAssemblyContaining<IScoringMonitoringDbContext>`) por un tipo de Application (p. ej. el propio `DependencyInjection`), quitar el registro en `Program.cs:32`. El DbContext concreto se inyecta directo en los query handlers. |

**Nota:** el explorador reportó que los índices únicos parciales del DbContext (dedup de crédito y de penalización) nunca se prueban porque los integration tests usan EF-InMemory. Follow-up separado (no bloquear C1): mover esos tests a Testcontainers Postgres como en los otros servicios.

---

## PR-4 · C1 en session-management (el grande — ~20 command handlers)

**Branch:** `refactor/session-persistence-seam`. Hacerlo **último**: el patrón ya estará validado dos veces y el outbox de MassTransit ya habrá estabilizado `SessionManagement.Infrastructure`.

| Commit | Contenido |
|---|---|
| 1 | (TDD) `ILiveSessionStore` con cargas de propósito que reemplazan los `.Include(...)` repetidos en handlers: `GetAsync(id)`, `GetWithTeamsAsync(id)`, `GetWithFlowAndTeamsAsync(id)` (auditar los ~4 shapes de Include reales antes de fijar la interfaz), `Add`, `SaveChangesAsync`. Fake in-memory para tests de handler. |
| 2–5 | Migrar command handlers **por feature folder**, un commit por grupo: (2) SessionLifecycle + LiveSessions, (3) SessionEnrollment, (4) EvidenceSubmissions + Penalties, (5) Hints. En cada grupo: reemplazar `IRepository<LiveSession>` + `IUnitOfWork` por `ILiveSessionStore`; los tests de handler existentes que mockeaban `IRepository` pasan al fake del store. Añadir el unit test que falta de `TransitionLiveSessionStateCommandHandler` (fan-out de hints al finalizar — hoy sin cobertura de orquestación). |
| 6 | Queries (8 handlers de LiveSessions/SessionEnrollment/SessionSnapshots) → `ISessionManagementDbContext` (que hoy tiene 0 consumidores; pasa a ser el seam de lectura real) o DbContext directo — elegir lo mismo que se eligió en scoring para consistencia. Borrar `IRepository<T>`/`Repository<T>`/`IUnitOfWork`. |
| 7 | Limpieza de arrastre: usings duplicados de `SessionManagement.Domain.LiveSessions`, registros DI muertos, `ILiveSessionStateNotifier` (0 refs) si aún existe tras el outbox. |

**Verificación por commit:** unit tests del grupo migrado. **Al final:** suite completa de integración + smoke del flujo evidencia→scoring→ranking en compose.

---

## Secuencia y dependencias

```
feature/masstransit-audit-outbox  ──merge──▶ develop
                                              │
                 PR-1 kernel split ───────────┤  (global, csproj-only, riesgo bajo)
                                              │
                 PR-2 mission ────────────────┤  (valida el patrón store)
                 PR-3 scoring ────────────────┤  (unifica el doble seam)
                 PR-4 session ────────────────┘  (el grande, patrón ya probado ×2)
```

- PR-1 es independiente de PR-2/3/4 (puede ir en paralelo), pero hacerlo primero evita que los PRs de C1 editen `.csproj` dos veces.
- PR-2/3/4 son independientes entre sí; el orden propuesto es por riesgo creciente.
- Cada PR: `--no-ff` a `develop`, build+test post-merge (regla del repo).

## Definition of done

- [ ] Ningún `*.Domain.csproj` referencia `Umbral.ServiceDefaults` (test de arquitectura lo fija).
- [ ] `Umbral.Kernel` sin PackageReference.
- [ ] Cero `IUnitOfWork` / `IRepository<T>` en el repo; cero `using Microsoft.EntityFrameworkCore` en command handlers.
- [ ] Todos los command handlers con unit test contra store fake (incluye los 6 de mission y `TransitionLiveSessionState` de session que hoy no tienen).
- [ ] mission-management ejecuta `UmbralLoggingBehavior`/`UmbralValidationBehavior` (C3 cerrado de paso).
- [ ] ADR-015 escrito; `repo-structure.md` actualizado (multi-csproj real).
- [ ] Suite completa verde + smoke compose del flujo principal.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| Conflictos con el branch de outbox en `SessionManagement/ScoringMonitoring.Infrastructure` | Prerrequisito duro: mergear outbox primero; PR-3/4 rebasean sobre ese estado |
| Cambiar shape de `.Include` al centralizar cargas en el store (lazy nulls) | Las cargas de propósito replican los Include actuales 1:1; integration tests con Postgres real como red |
| Tests que mockeaban `IRepository` quedan huérfanos | Se migran al fake del store en el mismo commit que su handler |
| Rename de namespace del kernel rompe medio repo | No se hace en PR-1; queda como follow-up mecánico opcional |
