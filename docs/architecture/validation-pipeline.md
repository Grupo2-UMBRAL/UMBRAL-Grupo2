# Validation Pipeline

Repositorio ahora tiene validacion reproducible para codigo, cobertura backend y smoke tests de `docker compose`.

## Scope

- install y build de `web` y `mobile`
- lint y typecheck de `web`
- typecheck y build de `mobile`
- build y tests `.NET` para `shared`, `user-management`, `mission-management`, `session-management` y `scoring-monitoring`
- cobertura backend con `Coverlet`
- reportes agregados de cobertura con `ReportGenerator`
- plan de adopcion de auditorias locales de mutacion con Stryker.NET, incorporadas gradualmente por servicio
- smoke tests de `docker compose` para `edge-proxy`, servicios backend, `Keycloak`, `PostgreSQL` y `RabbitMQ`
- scan basico para detectar secretos versionados fuera de archivos seed aprobados

## Local run

Loop angosto por area tocada:

```powershell
./scripts/Invoke-RepositoryValidation.ps1 -Scope Web -SkipComposeSmoke
./scripts/Invoke-RepositoryValidation.ps1 -Scope Mobile -SkipComposeSmoke
./scripts/Invoke-RepositoryValidation.ps1 -Scope Backend -SkipComposeSmoke
```

Gate unico de codigo antes de cerrar o integrar:

```powershell
./scripts/Invoke-RepositoryValidation.ps1 -SkipComposeSmoke
```

Smoke de `docker compose`:

```powershell
./scripts/Invoke-ComposeSmokeValidation.ps1
```

Si la maquina local tiene puertos ocupados o reservados por Windows, generar un env alterno y pasarlo al script:

```powershell
Copy-Item .env.example .env.validation
# Ajustar EDGE_PROXY_PORT, USER_MANAGEMENT_PORT, MISSION_MANAGEMENT_PORT,
# SESSION_OPERATIONS_PORT y SCORING_MONITORING_PORT a puertos disponibles.
./scripts/Invoke-ComposeSmokeValidation.ps1 -EnvironmentFilePath .env.validation
```

Validacion completa:

```powershell
./scripts/Invoke-RepositoryValidation.ps1
```

Politica operativa:

- durante implementacion, correr el scope mas angosto que pruebe el comportamiento tocado
- antes de cerrar o integrar, correr una sola vez el gate de codigo completo
- ampliar a compose smoke solo si el ticket toca integracion, infraestructura o salud del stack

Artefactos:

- `temp/validation/backend-coverage-summary.json`
- `temp/validation/TestResults/`
- `temp/validation/backend-coverage-report/index.html`
- `temp/validation/backend-coverage-report/Cobertura.xml`
- `temp/validation/backend-coverage-report/Summary.txt`
- `temp/validation/compose/compose-config.txt`
- `temp/validation/compose/compose-up.txt`
- `temp/validation/compose/compose-ps.txt`
- `temp/validation/compose/auth-smoke-tests.txt`

Cobertura backend:

- `coverlet.collector` genera resultados crudos `coverage.cobertura.xml` por proyecto de test bajo `temp/validation/TestResults/`.
- `ReportGenerator` agrega esos resultados en `temp/validation/backend-coverage-report/`.
- No hay exclusiones explicitas de assemblies, namespaces o archivos en este flujo. Si se agregan, deben quedar documentadas y justificadas aqui.

## Coverage policy

- **Meta academica (expectativa del profesor): `90%` de branch coverage del backend.** La meta es cobertura
  de **ramas** (branch), no de lineas.
- Umbral temporal actual que rompe el build: `10%`.
- Razon: repo todavia combina suites robustas en `user-management` con bootstrap tests minimos en otros servicios.
- Plan de subida:
  1. llevar cada servicio a casos de aplicacion y dominio medibles
  2. subir umbral temporal a `50%`
  3. cerrar cobertura de ramas de error y autorizacion
  4. subir gate final a `90%` de branch coverage

### Metrica del gate: branch coverage

`scripts/Get-BackendCoverageSummary.ps1` agrega y enforca **branch coverage** (`branches-covered` /
`branches-valid` del `coverage.cobertura.xml`), alineado con la meta academica. La line coverage se sigue
agregando y reportando como referencia secundaria (`OverallLineCoverage`), pero **no** es la metrica gateada.

Detalles:

- El agregado es por totales (`Σ branches-covered / Σ branches-valid`), no promedio de porcentajes por
  proyecto. Un proyecto sin ramas (`branches-valid = 0`) cuenta como `100%` vacuo y no arrastra el agregado.
- El JSON de salida trae `Metric = "branch"`, `OverallBranchCoverage`, `OverallLineCoverage` y por proyecto
  `BranchCoverage` + `LineCoverage`.
- El gate que rompe el build compara `OverallBranchCoverage` contra el umbral temporal (`10%` hoy). El
  `90%` sigue reportandose como "Academic target"; el paso final del plan de subida es enforcar `90%` branch
  subiendo el umbral (`-TemporaryCoverageThreshold` / `TargetCoverage` en `Invoke-RepositoryValidation.ps1`).

## Auditoria de mutacion local

Stryker.NET complementara la cobertura de ramas: introduce mutaciones temporales en el codigo bajo prueba y
confirma que la suite las detecte. Una mutacion que sobrevive no cambia el producto, pero indica una asercion
o caso de prueba insuficiente.

- Una vez configurada, la auditoria se ejecuta **localmente**, por alcance, y queda fuera de `Invoke-RepositoryValidation.ps1`.
- No se ejecuta en GitHub Actions por ahora. Se evaluara incorporarla a CI solo despues de tener una linea
  base, tiempos medidos y un umbral acordado.
- La herramienta debe versionarse como herramienta local de .NET; cada clon la restaura con `dotnet tool restore`.
- Los informes deben guardarse bajo `temp/validation/mutation/<alcance>/` y no se versionan.
- La cobertura de ramas sigue siendo la metrica de cobertura del gate. El resultado de mutacion se usa para
  priorizar nuevos casos de prueba que eleven tanto el score de mutacion como la cobertura de ramas.

Orden de adopcion deliberado:

1. `Umbral.ServiceDefaults`, empezando por `OpenApiExtensions` y sus pruebas directas.
2. Mission Management.
3. Session Management.
4. Scoring and Monitoring.
5. User Management.

Cada auditoria debe mutar un solo proyecto de produccion, ejecutar la suite de pruebas que lo cubre y registrar
el score, los mutantes sobrevivientes y los casos nuevos que se agregaron. No se establece un umbral bloqueante
hasta que exista una linea base para ese alcance.

## CI

Workflow: `.github/workflows/validation.yml`

Jobs:

- `code-validation`: secretos, frontend checks, build/test backend, cobertura
- `compose-smoke`: `docker compose config`, `up -d --build`, health checks, discovery de `Keycloak`, auth smokes

Stryker.NET no forma parte de estos jobs inicialmente; la politica de auditoria local se define arriba.

## Known gaps

- **Umbral de cobertura**: el gate ya mide branch coverage (meta `90%`) pero el umbral que rompe el build
  sigue en `10%` temporal; subir a `90%` es el paso final del plan de subida.
- **E2E de UI ausente**: `@playwright/test` esta como devDependency de `web` pero no hay specs ni script de
  e2e. La cobertura tipo E2E hoy es el smoke de auth por rol contra el edge-proxy (`infra/keycloak/verify/verify_auth.py`).
- `web` y `mobile` aun no tienen suites automatizadas de tests funcionales; por ahora el gate usa lint, typecheck y build.
- scan de secretos usa reglas conservadoras y allowlist explicita para seeds locales documentadas. Si aparecen nuevos fixtures de bootstrap, actualizar `scripts/Test-VersionedSecrets.ps1`.
