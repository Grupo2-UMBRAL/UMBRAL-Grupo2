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
|- scripts/
|- src/
|  |- apps/
|  |  |- edge-proxy/
|  |  |- mobile/
|  |  `- web/
|  |- shared/
|  `- services/
|     |- user-management/
|     |- mission-management/
|     |- scoring-monitoring/
|     `- session-management/
`- .agents/
   |- agents/
   `- skills/
```

## Convenciones

- `src/services/*/CONTEXT.md` mantiene el lenguaje de dominio por bounded context.
- `src/services/*/` contiene solo microservicios `.NET` alineados con bounded contexts.
- Cada servicio mantiene un solo proyecto `.Api` en esta fase; no se asume una particion inmediata en multiples `.csproj`.
- Dentro de cada servicio, el target interno es `Presentation`, `Application`, `Domain`, `Infrastructure`.
- `Presentation` aloja endpoints HTTP, adaptadores del borde de auth y hubs de `SignalR` cuando existan.
- `Application` coordina casos de uso y puede depender de `Domain`, pero no debe depender de `Infrastructure`.
- `Infrastructure` implementa persistencia, mensajeria, clientes externos y wiring tecnico para las capas internas.
- `src/shared/` contiene soporte tecnico cross-cutting que no pertenece a un bounded context.
- `src/shared/` no debe absorber contratos de negocio, modelos de dominio ni vocabulario propio de un bounded context.
- `src/apps/edge-proxy/` contiene el borde tecnico minimo para exponer entrada unificada sin meter logica de dominio.
- El gateway o `edge-proxy` es el borde publico, pero no reemplaza la validacion `JWT` ni la autorizacion propia de cada servicio.
- `src/apps/mobile/` contiene el shell mobile de `Participant` y sus clientes de auth, API y realtime.
- `src/apps/web/` contiene el frontend web cuando exista.
- `infra/` agrupa bootstrap operativo local como `Keycloak` y scripts de `PostgreSQL`.
- `scripts/` contiene automatizacion reproducible de validacion, smoke tests y soporte operativo del repo.
- `docker-compose.dev.yml` levanta el entorno de desarrollo distribuido.
- `docker-compose.utils.yml` levanta contenedores utilitarios para scaffolding y tareas de plantilla.
- `.agents/agents/` contiene instrucciones cortas para agentes.
- `.agents/skills/` contiene skills reutilizables del entorno.
- `docs/product/` contiene la version normalizada del enunciado y sus aclaratorias.
- `docs/architecture/adr/` contiene decisiones dificiles de revertir.

## Target de refactor documentado

Sin asumir carpetas que todavia no existen, la direccion acordada es esta:

- `src/services/` debe quedar reservado para bounded contexts y sus servicios.
- `src/shared/` aloja el codigo tecnico compartido movido fuera de `src/services/`.
- Cada servicio seguira en un solo `.Api` por ahora, pero con layering interno `Presentation`, `Application`, `Domain`, `Infrastructure`.

## No asumir todavia

- No asumir que `mobile` o `web` tengan implementacion completa de todas las capacidades del backlog.
- No asumir que los esqueletos `.NET` ya compilan localmente sin instalar SDK o restaurar paquetes.
- No asumir que el target de capas obliga hoy a separar proyectos por capa; el objetivo inmediato sigue siendo orden interno dentro de cada `.Api`.
- No asumir que el gateway sea duenio de la autorizacion de negocio; cada servicio conserva su propia frontera de validacion y autorizacion.
- No asumir pipelines de despliegue o gates adicionales mas alla de `.github/workflows/validation.yml` y `scripts/`.
