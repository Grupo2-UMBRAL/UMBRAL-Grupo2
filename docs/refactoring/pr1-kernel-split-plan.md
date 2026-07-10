# PR-1 · Split del shared kernel: `Umbral.Kernel` (sin framework) + `Umbral.ServiceDefaults` (defaults web)

> Detalle ejecutable del PR-1 de [plan-c1-c2-kernel-y-persistencia.md](plan-c1-c2-kernel-y-persistencia.md).
> Branch: `refactor/kernel-split` desde `develop`, **después** de mergear `feature/masstransit-audit-outbox`.

## Objetivo

Que ningún `*.Domain` compile contra ASP.NET Core, EF Core, Npgsql, MediatR ni FluentValidation (RNF-07). Hoy los 3 Domain persistentes (mission, session, scoring) referencian `Umbral.ServiceDefaults` — que arrastra todo eso — solo para usar `UmbralDomainException`, `UmbralTechnicalException` y `UmbralFailureCategory`.

## Decisión de diseño clave

**Los tipos movidos conservan el namespace `Umbral.ServiceDefaults`.**
Consecuencia: cero ediciones de archivos `.cs` en los servicios (29 archivos de Domain + handlers + validators siguen compilando tal cual). El PR toca solo `.csproj`, `.sln`, Dockerfiles, tests nuevos y docs. El rename de namespace a `Umbral.Kernel` queda como follow-up mecánico opcional (PR aparte, solo find-replace de `using`).

## Qué se mueve (verificado: solo dependen de BCL)

| Archivo | LOC | Nota |
|---|---|---|
| `UmbralServiceException.cs` | 21 | base abstracta (Code + Category) |
| `UmbralDomainException.cs` | 9 | hoja sealed |
| `UmbralTechnicalException.cs` | 9 | hoja sealed |
| `UmbralFailureCategory.cs` | 12 | enum de 7 valores |
| `UmbralRoles.cs` | 8 | ver "Decisión ADR" abajo |

**Se queda en `Umbral.ServiceDefaults`** (todo lo que toca framework): `AuthConfiguration`, `ServiceConfiguration`, `ServiceCollectionExtensions`, `KeycloakRoleClaimsTransformation`, `UmbralExceptionHandler`, `UmbralLoggingBehavior`, `UmbralValidationBehavior`. Estos consumen los tipos movidos vía la nueva referencia `ServiceDefaults → Kernel` sin cambiar una línea (mismo namespace).

**Decisión ADR:** `UmbralRoles` (Administrator/Operator/Participant) es lenguaje de autorización compartido — vocabulario de negocio en un módulo técnico, tensión con ADR-012. Se mueve al Kernel y se registra como **excepción consciente** en ADR-015: el Kernel puede alojar el vocabulario de autorización transversal; lo que sigue prohibido es contratos de bounded context, agregados o modelos de dominio.

## Estado inicial verificado (2026-07-10)

- `Directory.Build.props` centraliza `net10.0` + nullable + implicit usings + artifacts output → el `.csproj` del Kernel queda mínimo.
- CPM activo (`Directory.Packages.props`) — irrelevante para el Kernel (0 paquetes), pero explica por qué los `.csproj` no llevan versiones.
- 16 `.csproj` referencian `Umbral.ServiceDefaults` (15 de servicios/edge + su proyecto de tests). **Solo los 3 Domain cambian de referencia**; el resto no se toca (acceso transitivo a los tipos del Kernel vía `ServiceDefaults → Kernel`, las ProjectReference SDK-style fluyen transitivamente).
- `Umbral.sln`: formato clásico, 40 proyectos, con solution folders (`src/shared` existe como folder — `Umbral.ServiceDefaults` ya cuelga ahí).
- **Dockerfiles**: `infra/keycloak/docker/{user,mission,session,scoring-monitoring,edge-proxy}*.Dockerfile` copian `src/shared/Umbral.ServiceDefaults/Umbral.ServiceDefaults.csproj` a mano para cachear el `dotnet restore` (línea 9 en los 5). Sin la línea nueva del Kernel, `dotnet restore` falla dentro de la imagen.
- **CI/validación**: `scripts/Invoke-RepositoryValidation.ps1` tiene listas **hardcodeadas** de proyectos de test (líneas 36 y 52 incluyen `Umbral.ServiceDefaults.UnitTests`). Por eso los tests de arquitectura van en ese proyecto existente → **cero cambios al script**.

---

## Commits

### Commit 1 — `refactor(shared): extract framework-free Umbral.Kernel from ServiceDefaults`

1. Crear `src/shared/Umbral.Kernel/Umbral.Kernel.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Intencionalmente sin PackageReference: este proyecto es el núcleo
       sin framework que pueden referenciar los Domain (RNF-07, ADR-015). -->
</Project>
```

2. `git mv` de los 5 archivos a `src/shared/Umbral.Kernel/` (namespace intacto).

3. `Umbral.ServiceDefaults.csproj` añade:

```xml
<ItemGroup>
  <ProjectReference Include="..\Umbral.Kernel\Umbral.Kernel.csproj" />
</ItemGroup>
```

4. Añadir al sln bajo el folder existente:

```
dotnet sln Umbral.sln add --solution-folder shared src/shared/Umbral.Kernel/Umbral.Kernel.csproj
```

(Verificar contra el nombre real del solution folder en el `.sln`; si `dotnet sln` crea un folder duplicado, editar el `.sln` a mano para colgarlo del GUID del folder `shared` existente.)

5. **Los 5 Dockerfiles** (`infra/keycloak/docker/*.Dockerfile`): añadir junto a la línea 9 existente:

```dockerfile
COPY src/shared/Umbral.Kernel/Umbral.Kernel.csproj src/shared/Umbral.Kernel/
```

**Verificación:** `dotnet build Umbral.sln` + `docker compose -f docker-compose.dev.yml build session-management-service` (basta una imagen para validar el patrón de COPY; las 5 se validan en el smoke final).

### Commit 2 — `refactor(domain): point Domain projects at Umbral.Kernel instead of ServiceDefaults`

En los 3 `.csproj` (mission, session, scoring — user-management Domain no referencia nada, no se toca):

```diff
- <ProjectReference Include="..\..\..\shared\Umbral.ServiceDefaults\Umbral.ServiceDefaults.csproj" />
+ <ProjectReference Include="..\..\..\shared\Umbral.Kernel\Umbral.Kernel.csproj" />
```

Nada más cambia: los `using Umbral.ServiceDefaults;` de los 29 archivos de Domain resuelven contra el Kernel.

**Riesgo a vigilar en este commit:** si algún archivo de Domain usa algo de ServiceDefaults **además** de los 5 tipos movidos, el compilador lo delata aquí. (Grep previo dice que no: solo excepciones/categoría.)

**Verificación:** `dotnet build Umbral.sln` + unit tests de dominio de los 3 servicios:

```
dotnet test src/services/mission-management/tests/MissionManagement.UnitTests
dotnet test src/services/session-management/tests/SessionManagement.UnitTests
dotnet test src/services/scoring-monitoring/tests/ScoringMonitoring.UnitTests
```

### Commit 3 — `test(shared): lock kernel purity and Domain dependency direction`

Nuevo archivo `src/shared/Umbral.ServiceDefaults.UnitTests/ArchitectureInvariantsTests.cs` (proyecto existente → el script de validación y CI lo recogen sin cambios). Dos invariantes, ambos basados en archivos (no requieren referenciar los Domain):

```csharp
// Bosquejo — ajustar a estilo xunit del proyecto
public sealed class ArchitectureInvariantsTests
{
    // 1. Umbral.Kernel no declara ningún PackageReference ni FrameworkReference.
    [Fact]
    public void Kernel_declares_no_package_or_framework_references()
    {
        var csproj = XDocument.Load(Path.Combine(RepoRoot(), "src/shared/Umbral.Kernel/Umbral.Kernel.csproj"));
        Assert.Empty(csproj.Descendants("PackageReference"));
        Assert.Empty(csproj.Descendants("FrameworkReference"));
    }

    // 2. Ningún *.Domain.csproj referencia ServiceDefaults ni paquetes de framework.
    [Theory]
    [MemberData(nameof(DomainProjects))] // glob src/services/*/[A-Z]*.Domain/*.csproj
    public void Domain_projects_reference_only_the_kernel(string domainCsprojPath) { /* … */ }

    // RepoRoot(): subir desde AppContext.BaseDirectory hasta encontrar Umbral.sln.
    // Con UseArtifactsOutput los binarios viven en artifacts/ en la raíz del repo,
    // así que la subida es corta; fallar con mensaje claro si no se encuentra.
}
```

Nota TDD: el invariante 2 está rojo si se ejecuta antes del commit 2 (así se validó localmente); en la historia queda después para que cada commit deje CI verde. Es un test-candado de regresión, no un test de conducta.

**Verificación:** `./scripts/Invoke-RepositoryValidation.ps1 -Scope BackendUnit -SkipComposeSmoke` (la misma fast lane de CI).

### Commit 4 — `docs(adr): ADR-015 kernel split + refresh repo-structure`

1. `docs/architecture/adr/ADR-015-framework-free-shared-kernel.md`:
   - **Decision:** dos proyectos compartidos con reglas de dependencia distintas. `Umbral.Kernel`: sin dependencias, referenciable por cualquier capa incluido Domain; contiene la jerarquía de excepciones (`UmbralServiceException` + hojas + `UmbralFailureCategory`) y `UmbralRoles`. `Umbral.ServiceDefaults`: defaults web (auth, behaviors, telemetry, DI de EF/Npgsql), referenciable solo por Application/Infrastructure/Api.
   - **Excepción explícita a ADR-012:** `UmbralRoles` es vocabulario de autorización transversal y vive en el Kernel; sigue prohibido mover contratos de bounded context, agregados o lenguaje de dominio propio de un contexto.
   - **Guardrails:** `Umbral.Kernel` nunca gana un `PackageReference` (test-candado lo fija); `*.Domain` solo puede referenciar `Umbral.Kernel`.
   - **Follow-ups registrados, no ejecutados:** (a) rename de namespace `Umbral.ServiceDefaults` → `Umbral.Kernel` en los tipos movidos; (b) colapsar las dos hojas de excepción en un solo tipo concreto (hoy `UmbralValidationBehavior` lanza `UmbralDomainException` con categoría `Validation` — el tipo y la categoría son discriminadores redundantes).
2. `docs/architecture/repo-structure.md`: actualizar la sección de convenciones — hoy dice "cada servicio mantiene un solo proyecto `.Api`" y "no asumir partición en múltiples `.csproj`", lo cual ya no describe la realidad (4 `.csproj` por servicio desde hace semanas). Describir el layout real: `src/shared/Umbral.Kernel` + `src/shared/Umbral.ServiceDefaults`, y la regla de dependencia por capa.
3. ADR-012: añadir nota "Superseded in part by ADR-015" en la sección de related decisions (no reescribir el ADR).

**Verificación:** lectura; no toca código.

---

## Verificación final del PR (antes del merge)

```powershell
# 1. Suite completa local (igual que CI code-validation)
./scripts/Invoke-RepositoryValidation.ps1 -SkipComposeSmoke

# 2. Las 5 imágenes buildean con el COPY nuevo
docker compose -f docker-compose.dev.yml build

# 3. Smoke del stack
docker compose -f docker-compose.dev.yml up -d
# health endpoints de los 4 servicios + edge; luego down
```

Merge: `git merge --no-ff refactor/kernel-split` a `develop` y **build+test inmediato post-merge** (regla del repo: un no-ff ya tragó usings silenciosamente una vez).

## Checklist de done

- [ ] `Umbral.Kernel` existe, 5 tipos movidos, 0 PackageReference/FrameworkReference.
- [ ] `mission/session/scoring *.Domain.csproj` referencian solo `Umbral.Kernel`.
- [ ] Cero ediciones de `.cs` en servicios (solo el test nuevo en shared).
- [ ] 5 Dockerfiles con el `COPY` del Kernel; `docker compose build` verde.
- [ ] Test-candado de arquitectura en `Umbral.ServiceDefaults.UnitTests` (recogido por el script de validación sin editarlo).
- [ ] ADR-015 escrito; ADR-012 anotado; `repo-structure.md` refleja la realidad multi-csproj.
- [ ] Validación completa + compose smoke verdes; merge `--no-ff` + build+test post-merge.

## Riesgos y rollback

| Riesgo | Mitigación |
|---|---|
| Restore falla dentro de Docker por el csproj nuevo no copiado | El COPY va en el **mismo commit 1**; se valida con `compose build` antes de seguir |
| `dotnet sln add` cuelga el proyecto de un folder duplicado | Revisar el `.sln` en el diff del commit 1; editar GUID a mano si hace falta |
| Algún consumidor usaba ServiceDefaults por transitividad rara | El compilador lo delata en commit 2; el fix es añadir la referencia directa que faltaba, no revertir |
| Coverage: los 5 tipos ahora cuentan bajo el assembly `Umbral.Kernel` | Lógica mínima (ctors + enum); si el reporte de cobertura por assembly protesta, incluir el Kernel en el mismo bucket de shared en el reporte — no gate real |
| Rollback | El PR es estructural puro: revert del merge commit restaura el estado previo sin migraciones ni datos de por medio |

## Fuera de alcance (explícito)

- Rename de namespace de los tipos movidos.
- Colapso de la jerarquía de excepciones.
- Cualquier cambio en handlers, DI de servicios o comportamiento en runtime.
- C1 (persistencia) y C3 (pipeline MediatR de mission) — PRs siguientes.
