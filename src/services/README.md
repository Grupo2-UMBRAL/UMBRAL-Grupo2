# Backend Foundation

Este directorio contiene el baseline backend ejecutable para los bounded contexts que hoy se implementan con servicios `.NET`.

## Servicios incluidos

- `identity-access/Umbral.IdentityAccess.Api`
- `mission-design/Umbral.MissionDesign.Api`
- `session-operations/Umbral.SessionOperations.Api`
- `scoring-audit/Umbral.ScoringAudit.Api`

## Capas esperadas por servicio

- entrada HTTP en `Program.cs` y endpoints
- aplicacion con comandos y queries `MediatR` bajo `Application/`
- persistencia e integraciones tecnicas bajo `Infrastructure/`
- contexto de dominio gobernado por `CONTEXT.md` del bounded context

## Convencion de errores HTTP

Los servicios usan `UmbralExceptionHandler` para mapear fallos de forma consistente:

- `Validation` -> `400`
- `Unauthorized` -> `401`
- `Forbidden` -> `403`
- `NotFound` -> `404`
- `Conflict` -> `409`
- `Domain` -> `422`
- `Technical` -> `500`

## Build y tests

### Identity and Access

```powershell
dotnet build src/services/identity-access/Umbral.IdentityAccess.Api/Umbral.IdentityAccess.Api.csproj
dotnet test src/services/identity-access/Umbral.IdentityAccess.Api.Tests/Umbral.IdentityAccess.Api.Tests.csproj
```

### Mission Design

```powershell
dotnet build src/services/mission-design/Umbral.MissionDesign.Api/Umbral.MissionDesign.Api.csproj
dotnet test src/services/mission-design/Umbral.MissionDesign.Api.Tests/Umbral.MissionDesign.Api.Tests.csproj
```

### Session Operations

```powershell
dotnet build src/services/session-operations/Umbral.SessionOperations.Api/Umbral.SessionOperations.Api.csproj
dotnet test src/services/session-operations/Umbral.SessionOperations.Api.Tests/Umbral.SessionOperations.Api.Tests.csproj
```

### Scoring and Audit

```powershell
dotnet build src/services/scoring-audit/Umbral.ScoringAudit.Api/Umbral.ScoringAudit.Api.csproj
dotnet test src/services/scoring-audit/Umbral.ScoringAudit.Api.Tests/Umbral.ScoringAudit.Api.Tests.csproj
```

### Shared technical defaults

```powershell
dotnet test src/shared/Umbral.ServiceDefaults.Tests/Umbral.ServiceDefaults.Tests.csproj
```

## Migraciones EF Core

Cada servicio deja preparado:

- `DbContext` con schema propio
- factory de diseño `IDesignTimeDbContextFactory`
- historial de migraciones en el schema del servicio
- flag `Persistence:ApplyMigrationsOnStartup`

Ejemplos:

```powershell
dotnet ef migrations add InitialSchemaBaseline --project src/services/mission-design/Umbral.MissionDesign.Api/Umbral.MissionDesign.Api.csproj --startup-project src/services/mission-design/Umbral.MissionDesign.Api/Umbral.MissionDesign.Api.csproj
dotnet ef migrations add InitialSchemaBaseline --project src/services/session-operations/Umbral.SessionOperations.Api/Umbral.SessionOperations.Api.csproj --startup-project src/services/session-operations/Umbral.SessionOperations.Api/Umbral.SessionOperations.Api.csproj
dotnet ef migrations add InitialSchemaBaseline --project src/services/scoring-audit/Umbral.ScoringAudit.Api/Umbral.ScoringAudit.Api.csproj --startup-project src/services/scoring-audit/Umbral.ScoringAudit.Api/Umbral.ScoringAudit.Api.csproj
```

## Validacion integrada

La validacion reproducible del repo vive en `scripts/Invoke-RepositoryValidation.ps1`.

- backend: `dotnet build`, `dotnet test` y cobertura `XPlat Code Coverage`
- smoke distribuido: `scripts/Invoke-ComposeSmokeValidation.ps1`
- politica y artefactos: `docs/architecture/validation-pipeline.md`
