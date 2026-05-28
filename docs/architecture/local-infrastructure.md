# Local Infrastructure Bootstrap

Este documento aterriza la fase local hasta el paso 4 acordado:

1. infraestructura local base
2. `edge proxy` minimo
3. bootstrap de identidad
4. esqueleto tecnico por servicio

## Componentes levantados

- `postgres`
- `rabbitmq`
- `keycloak`
- `mission-design-service`
- `session-operations-service`
- `scoring-audit-service`
- `edge-proxy`

## Puertos locales

- `edge-proxy`: `7000`
- `keycloak`: `8080`
- `mission-design-service`: `7101`
- `session-operations-service`: `7102`
- `scoring-audit-service`: `7103`
- `postgres`: `5432`
- `rabbitmq`: `5672`
- `rabbitmq management`: `15672`

## Rutas del edge proxy

- `/mission-design/*` -> `mission-design-service`
- `/session-operations/*` -> `session-operations-service`
- `/session-hub/*` -> hub `SignalR` de `session-operations-service`
- `/scoring-audit/*` -> `scoring-audit-service`
- `/auth/*` -> `Keycloak`

## Bootstrap de identidad

Archivo fuente:

- `infra/keycloak/import/umbral-realm.json`

Incluye:

- realm `umbral`
- roles `Administrator`, `Operator`, `Participant`
- clients iniciales para `web`, `mobile`, APIs y `edge proxy`
- usuarios semilla `admin`, `operator`, `participant`

Las credenciales incluidas en `.env.example`, `appsettings.json` y el realm de Keycloak son semillas locales. No deben reutilizarse en ambientes compartidos, staging ni produccion.

## Nota sobre tokens JWT en local

Los servicios dentro de Docker validan tokens contra:

- `http://keycloak:8080/realms/umbral`

Los clientes que corran en el host suelen descubrir Keycloak por:

- `http://localhost:8080/realms/umbral`
- o por el proxy `http://localhost:7000/auth/realms/umbral`

En fases posteriores hay que fijar una estrategia unica de issuer para evitar que un token emitido con host `localhost` sea rechazado por servicios configurados con host interno `keycloak`. Hasta cerrar esa decision, probar autenticacion desde el mismo contexto de red indicado por la configuracion activa.

Para `SignalR`, el servicio `Session Operations` acepta el token JWT en el query string `access_token` solo para el hub `/hubs/session`, que es el patron esperado para conexiones WebSocket autenticadas.

## Persistencia local

Se usa una sola instancia de `PostgreSQL` con esquemas separados:

- `mission_design`
- `session_operations`
- `scoring_audit`

Eso mantiene alineacion con `ADR-006` sin abrir complejidad operativa innecesaria en esta fase.

## Estado del scaffold `.NET`

Cada servicio incluye:

- `Program.cs`
- autenticacion `JWT`
- autorizacion base
- `MediatR`
- `EF Core`
- `DbContext` vacio
- endpoint `/health`
- endpoint protegido `/api/.../bootstrap`

`Session Operations` agrega:

- `SignalR`
- hub `/hubs/session`

## Pre-requisitos para ejecutarlo

- `Docker` y `Docker Compose`
- acceso a internet para descargar imagenes base y paquetes NuGet durante el build en contenedores

## Arranque sugerido

1. copiar `.env.example` a `.env`
2. ejecutar `docker compose -f docker-compose.dev.yml up --build`
3. probar `http://localhost:7000/health`
4. probar `http://localhost:7000/auth/realms/umbral/.well-known/openid-configuration`

## Compose de utilidades

Archivo:

- `docker-compose.utils.yml`

Servicios:

- `dotnet-sdk`: contenedor persistente para entrar al workspace y ejecutar comandos manuales
- `template-generator`: utilidad enfocada en `dotnet new` y scaffolding
- `repo-shell`: shell interactivo sobre el repo con SDK de `.NET`

Ejemplos:

1. listar plantillas disponibles:
   `docker compose -f docker-compose.utils.yml run --rm template-generator`
2. generar un proyecto web API:
   `docker compose -f docker-compose.utils.yml run --rm --entrypoint dotnet template-generator new webapi -n Sample.Api -o src/sample/Sample.Api`
3. abrir shell utilitario:
   `docker compose -f docker-compose.utils.yml run --rm repo-shell`

## Lo que todavia no esta implementado

- migraciones reales de `EF Core`
- entidades de dominio
- contratos REST de negocio
- consumidores o publishers de `RabbitMQ`
- autorizacion fina por permisos
- clientes `web` y `mobile`
