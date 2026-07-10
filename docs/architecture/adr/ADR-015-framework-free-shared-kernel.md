# ADR-015: Kernel compartido sin framework (`Umbral.Kernel`) separado de `Umbral.ServiceDefaults`

## Status

Accepted (supersede en parte a [ADR-012](ADR-012-service-internal-layering-and-shared-kernel-placement.md))

## Context

`ADR-012` fijó `src/shared` como el hogar del código técnico transversal y colocó allí un único
proyecto, `Umbral.ServiceDefaults`. Ese proyecto arrastra todo el stack web: `FrameworkReference`
a `Microsoft.AspNetCore.App` más paquetes de `MediatR`, `FluentValidation`, EF Core, `Npgsql` y
OpenTelemetry.

El problema aparece con la capa de dominio. Los tres `Domain` persistentes (`mission`, `session`,
`scoring`) referenciaban `Umbral.ServiceDefaults` **solo** para usar cinco tipos que dependen
únicamente de la BCL:

- `UmbralServiceException` (base abstracta: `Code` + `Category`)
- `UmbralDomainException` y `UmbralTechnicalException` (hojas selladas)
- `UmbralFailureCategory` (enum de 7 valores)
- `UmbralRoles` (constantes `Administrator` / `Operator` / `Participant`)

Esa referencia hacía que cada `Domain` compilara transitivamente contra ASP.NET Core, EF Core,
`Npgsql`, `MediatR` y `FluentValidation` — una violación de RNF-07 (el dominio no debe depender de
infraestructura ni de framework). El `Domain` no usaba nada de ese stack; solo esos cinco tipos.

## Decision

Escindir `src/shared` en **dos proyectos con reglas de dependencia distintas**:

1. **`Umbral.Kernel`** — núcleo sin framework. Cero `PackageReference` y cero `FrameworkReference`
   (TFM, nullable e implicit usings los aporta `Directory.Build.props`). Referenciable por
   **cualquier** capa, incluido `Domain`. Contiene la jerarquía de excepciones
   (`UmbralServiceException` + las dos hojas + `UmbralFailureCategory`) y `UmbralRoles`.

2. **`Umbral.ServiceDefaults`** — defaults web: auth/JWT, `KeycloakRoleClaimsTransformation`,
   behaviors de logging/validación de `MediatR`/`FluentValidation`, `UmbralExceptionHandler`,
   telemetría y DI de EF/`Npgsql`. Referencia a `Umbral.Kernel` y **solo** puede ser referenciado
   por `Application`, `Infrastructure` y `Api`.

Los cinco tipos movidos **conservan el namespace `Umbral.ServiceDefaults`**. Consecuencia
deliberada: cero ediciones de archivos `.cs` en los servicios — los `using` existentes resuelven
contra el Kernel a través de la nueva `ProjectReference`. Los `Domain` cambian una sola línea de
`.csproj` (de `ServiceDefaults` a `Kernel`).

### Excepción explícita a ADR-012

La regla 9 de ADR-012 prohíbe que `src/shared` absorba vocabulario de negocio. `UmbralRoles`
es lenguaje de **autorización transversal**, no vocabulario de un bounded context concreto, y lo
consumen tanto el borde web como los servicios. Se aloja en `Umbral.Kernel` como excepción
consciente. Sigue prohibido mover al Kernel (o a cualquier `src/shared`) contratos de bounded
context, agregados o lenguaje de dominio propio de un contexto.

## Consequences

Positivas:

- Ningún `*.Domain` compila ya contra ASP.NET Core, EF Core, `Npgsql`, `MediatR` ni
  `FluentValidation` (RNF-07 satisfecho).
- El movimiento es estructural puro: sin cambios de comportamiento en runtime, sin migraciones,
  sin tocar handlers ni DI de servicios.
- La dirección de dependencia queda fijada por un test-candado (ver Guardrails), no por disciplina
  manual.

Negativas / coste:

- Un proyecto más en la solución y una línea `COPY` extra del `.csproj` del Kernel en los cinco
  Dockerfiles de servicio (la capa de `dotnet restore` en imagen necesita el manifiesto).
- Los cinco tipos ahora cuentan bajo el assembly `Umbral.Kernel` en los reportes de cobertura
  (lógica mínima: ctors + enum + constantes).

## Guardrails

- `Umbral.Kernel` nunca gana un `PackageReference` ni un `FrameworkReference`.
- `*.Domain` solo puede referenciar `Umbral.Kernel` dentro de `src/shared` (nunca
  `Umbral.ServiceDefaults`).
- Ambos invariantes están fijados por `ArchitectureInvariantsTests` en
  `Umbral.ServiceDefaults.UnitTests` (assertions basadas en el grafo de `.csproj`).

## Follow-ups (registrados, no ejecutados en este PR)

- **Rename de namespace:** mover los cinco tipos de `Umbral.ServiceDefaults` a `Umbral.Kernel`.
  Es un find-replace mecánico de `using` en su propio PR; se difiere para mantener este cambio
  libre de ediciones `.cs` en los servicios.
- **Colapso de la jerarquía de excepciones:** hoy `UmbralValidationBehavior` lanza
  `UmbralDomainException` con `UmbralFailureCategory.Validation` — el tipo concreto y la categoría
  son discriminadores redundantes. Evaluar unificar las dos hojas en un solo tipo con `Category`
  como único discriminador.

## Related

- Supersede en parte a [ADR-012](ADR-012-service-internal-layering-and-shared-kernel-placement.md).
- Detalle de ejecución: `docs/refactoring/pr1-kernel-split-plan.md`.
