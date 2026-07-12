# Handoff — C1 (colapso del trío de persistencia) · para el próximo agente

> Continúa el plan maestro [plan-c1-c2-kernel-y-persistencia.md](plan-c1-c2-kernel-y-persistencia.md).
> **C2 (PR-1, kernel split) ya está HECHO y mergeado.** Tu trabajo empieza en **PR-2 (C1 en mission-management)**.
> Fecha del handoff: 2026-07-10. Branch base: `develop` (limpio, verde).

## 1. Qué ya está hecho (no lo rehagas)

**PR-1 · C2 kernel split** — mergeado a develop (`c0cde74`). Entregó:
- `src/shared/Umbral.Kernel` (0 paquetes) con los 5 tipos BCL (`UmbralServiceException` + 2 hojas, `UmbralFailureCategory`, `UmbralRoles`). **Conservan el namespace `Umbral.ServiceDefaults`** → cero ediciones `.cs` en servicios.
- mission/session/scoring `*.Domain.csproj` referencian **solo `Umbral.Kernel`**.
- `ArchitectureInvariantsTests` en `Umbral.ServiceDefaults.UnitTests` fija: Kernel sin PackageReference y ningún Domain→ServiceDefaults. Lo recoge `Invoke-RepositoryValidation.ps1` sin editar el script.
- ADR-015 escrito; ADR-012 marcado superseded-in-part; `repo-structure.md` al layout real (4 csproj/servicio).

**Arreglos colaterales que hice sobre develop** (pre-existentes, no de C1/C2):
- `b8d125e` — `CreateParticipantCommandHandlerTests.cs` no compilaba (FluentAssertions sin paquete + `participantUser` sin declarar, desde `e2d8fd4`). Reparado a xUnit Assert.
- `8fa1fc9` + `c261f28` — un **Gmail App Password real** estaba versionado en `appsettings.Development.json`. Movido a `.env` (git-ignored, `SMTP_*` → compose inyecta `Email__Smtp__*`); 2 falsos positivos del escáner de secretos corregidos. Lane de validación ahora verde.
  - ⚠️ **Acción pendiente del usuario (externa):** rotar esa App Password en la cuenta Google — sigue en el historial de git.

**Estado de verificación de develop hoy:** `dotnet build` 0 errores · `Invoke-RepositoryValidation.ps1 -Scope BackendUnit -SkipComposeSmoke` → **exit 0** · 5 imágenes docker buildean.

## 2. Tu misión: PR-2 · C1 en mission-management

Es el caso más simple; **valida el patrón `store`** que luego repites en scoring (PR-3) y session (PR-4). Ver detalle commit-a-commit en el plan maestro (sección PR-2). Resumen de diseño:

- **Comandos** → nueva interfaz de propósito por agregado `IMissionStore` (Application/Abstractions): `Task<Mission?> GetAsync(Guid, ct)`, `void Add(Mission)`, `Task SaveChangesAsync(ct)`. Implementación `MissionStore` en Infrastructure, **absorbe lo que hoy hace `MissionLoader`**. Los command handlers quedan 100% mockeables → EF desaparece de sus archivos.
- **Queries** → siguen leyendo del DbContext (CQRS, ADR-006): se quedan en `IMissionManagementDbContext`. No reescribas proyecciones.
- **Borra** `IUnitOfWork`, `IRepository<T>`, `Repository<T>` y sus tests.
- **TDD:** empieza en rojo con el fake de `IMissionStore` + unit tests de los 4 command handlers.

## 3. Estado real de mission-management (verificado 2026-07-10)

**4 command handlers** (`.../Features/Missions/Commands/{Create,Update,Activate,Deactivate}Mission/`).
**4 query handlers** (`.../Queries/{GetMissionById,ListMissions,GetEligibleMissionForLiveSession,ListEligibleMissionsForLiveSession}/`).

Seam de persistencia actual:
- `MissionManagement.Application/Abstractions/`: `IUnitOfWork.cs`, `IRepository.cs`, `IMissionManagementDbContext.cs`.
- `MissionManagement.Application/Features/Missions/MissionLoader.cs` — **estático**, opera sobre `IMissionManagementDbContext`. Métodos: `LoadAsync` (fetch plano de rows + `Mission.Rehydrate`), `RequireAsync` (lanza `UmbralDomainException` `mission_not_found`), `DeleteItemsAsync`, `AddItems`. Esto es lo que `MissionStore` debe encapsular.
- `MissionManagement.Infrastructure/Persistence/`: `Repository.cs`, `MissionManagementDbContext.cs`.

**Cobertura de tests hoy:** solo `UpdateMissionCommandHandlerTests.cs` existe entre los command handlers. **Create/Activate/Deactivate NO tienen unit test** — créalos en el commit 1 (TDD, rojo). (También query handlers sin test.)

**Deuda a limpiar de paso (plan PR-2 commit 2):** `DeactivateMissionCommandHandler` carga el agregado completo con queries de más (copy-paste de Activate); al migrar al store, que solo cargue lo necesario.

## 4. ⚠️ C3 (bug real) — ciérralo en este PR

`MissionManagement.Api/Program.cs:9` hace:
```csharp
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<IMissionManagementDbContext>());
```
Registra los handlers por assembly-scan **pero NUNCA llama a `AddMissionManagementApplication()`** (el composition root en `MissionManagement.Application/DependencyInjection.cs`, que es quien añade `UmbralLoggingBehavior` y `UmbralValidationBehavior`). **Efecto: en mission el pipeline de logging/validación no corre.** Los otros 3 servicios sí llaman a su `Add<Svc>Application()`.

Fix: sustituir la línea 9 por `builder.Services.AddMissionManagementApplication();` (y mantener `AddMissionManagementInfrastructure` en la 10). Toca la misma zona que el registro del `MissionStore`, por eso va en este PR.

## 5. Reglas del repo (obligatorias)

- **TDD**, commits pequeños (AGENTS.md). Merge **`--no-ff`** a develop + **build+test inmediato post-merge** (regla dura tras el incidente de usings tragados).
- Verificación PR-2: `dotnet test` unit + **integration de mission (Testcontainers Postgres = red de seguridad de conducta)** + smoke docker compose.
- Validación local: `./scripts/Invoke-RepositoryValidation.ps1 -Scope BackendUnit -SkipComposeSmoke` (fast lane). El `-Scope Full` ya vuelve a estar verde (secreto resuelto).
- Vocabulario front: **etapa / misión / sesión** (no aplica a backend, pero por si tocas contratos).

## 6. Después de PR-2

- **PR-3** scoring-monitoring (unifica el doble seam `IApplyPenaltyScoreboardStore`→`IScoreboardStore`; borra `IScoringMonitoringDbContext` como abstracción). Rebase sobre el outbox ya mergeado.
- **PR-4** session-management (el grande, ~20 command handlers; migra por feature folder). Último, con el patrón ya probado ×2.
- Definition of done global y riesgos: sección final del plan maestro.

**Follow-ups de C2 registrados en ADR-015 (NO son parte de C1):** rename de namespace `Umbral.ServiceDefaults`→`Umbral.Kernel` en los tipos movidos; colapsar las 2 hojas de excepción en un solo tipo con `Category` como único discriminador.
