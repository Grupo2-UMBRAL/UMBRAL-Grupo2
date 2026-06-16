# Template Utilities

Utilidades pensadas para usarse con `docker-compose.utils.yml`.

## Casos base

Listar plantillas disponibles:

```bash
docker compose -f docker-compose.utils.yml run --rm template-generator
```

Generar un nuevo proyecto Web API:

```bash
docker compose -f docker-compose.utils.yml run --rm --entrypoint dotnet template-generator new webapi -n Sample.Api -o src/sample/Sample.Api
```

Generar una solution:

```bash
docker compose -f docker-compose.utils.yml run --rm --entrypoint dotnet template-generator new sln -n Umbral
```

Abrir shell con SDK:

```bash
docker compose -f docker-compose.utils.yml run --rm repo-shell
```
