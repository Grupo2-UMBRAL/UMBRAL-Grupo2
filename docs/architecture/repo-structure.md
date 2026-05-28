# Repo Structure

Estructura real actual del repositorio.

```text
/
|- .env.example
|- docker-compose.yml
|- docker-compose.dev.yml
|- docker-compose.utils.yml
|- infra/
|  |- keycloak/
|  `- postgres/
|- docs/
|  |- architecture/
|  `- product/
|- src/
|  |- apps/
|  |  |- edge-proxy/
|  |  `- web/
|  `- services/
|     |- identity-access/
|     |- mission-design/
|     |- scoring-audit/
|     `- session-operations/
`- .agents/
   |- agents/
   `- skills/
```

## Convenciones

- `src/services/*/CONTEXT.md` mantiene el lenguaje de dominio por bounded context.
- `src/services/*/` contiene los microservicios `.NET` que implementan cada bounded context.
- `src/apps/edge-proxy/` contiene el borde tecnico minimo para exponer entrada unificada sin meter logica de dominio.
- `src/apps/web/` contiene el frontend web cuando exista.
- `infra/` agrupa bootstrap operativo local como `Keycloak` y scripts de `PostgreSQL`.
- `docker-compose.dev.yml` levanta el entorno de desarrollo distribuido.
- `docker-compose.utils.yml` levanta contenedores utilitarios para scaffolding y tareas de plantilla.
- `.agents/agents/` contiene instrucciones cortas para agentes.
- `.agents/skills/` contiene skills reutilizables del entorno.
- `docs/product/` contiene la version normalizada del enunciado y sus aclaratorias.
- `docs/architecture/adr/` contiene decisiones dificiles de revertir.

## No asumir todavia

- No asumir que `mobile` ya existe en este repo ni que `web` tenga implementacion completa.
- No asumir que los esqueletos `.NET` ya compilan localmente sin instalar SDK o restaurar paquetes.
- No asumir pipelines de `CI/CD` o tests automatizados hasta que se agreguen explicitamente.
