# Infrastructure Outline

> **Estado: bosquejo temprano, parcialmente superado.** Este documento fijó la intención inicial. Dos
> secciones ya no reflejan `develop` y están corregidas más abajo: (1) el layering interno por servicio ya
> no es "un solo proyecto `.Api`" sino cuatro proyectos por servicio (ver [ADR-015](adr/ADR-015-framework-free-shared-kernel.md)
> y `repo-structure.md`); (2) el CI ya existe (`.github/workflows/validation.yml`, ver `validation-pipeline.md`),
> no es "futuro". La topología, las fronteras y la mensajería siguen vigentes.

Bosquejo inicial de la infraestructura de UMBRAL para una arquitectura de microservicios real desde el dia uno, con integracion hibrida: comunicacion sincronica como mecanismo principal y mensajeria asincrona solo para responsabilidades secundarias. Incluye desarrollo local en `docker-compose` y evolucion futura hacia despliegue en nube y `CI/CD`.

## Objetivo

Definir los componentes minimos necesarios para empezar a implementar la solucion bajo un enfoque de microservicios reales, manteniendo compatibilidad con despliegue local, nube y automatizacion futura.

## Principios

- Empezar con microservicios reales, no con un monolito modular disfrazado.
- Separar capacidades de negocio por bounded context.
- Diferenciar modulos logicos de tecnologias concretas.
- Usar comunicacion sincronica como camino principal entre servicios.
- Reservar la mensajeria asincrona para procesos secundarios donde realmente aporte valor.
- Preparar el sistema para evolucion en nube y `CI/CD`.

## Arquitectura logica

Los microservicios del sistema se alinean con los bounded contexts y capacidades principales:

- `Mission Design Service`
- `Session Operations Service`
- `Scoring and Monitoring Service`
- `Identity and Access Service`

La implementacion actual de `Identity and Access Service` se apoya en `Keycloak` como proveedor de identidad.

## Componentes base

### 1. Mission Design Service

Responsabilidad:

- gestionar misiones
- gestionar stages, nodos, subetapas y pistas
- soportar reutilizacion de plantillas de stages
- exponer datos base para creacion de sesiones

Tecnologia esperada:

- `.NET`
- `MediatR`
- `EF Core`

### 2. Session Operations Service

Responsabilidad:

- gestionar sesiones en vivo
- gestionar equipos y progreso
- aceptar evidencias operativas
- coordinar el flujo en tiempo real
- publicar eventos del dominio operativo

Tecnologia esperada:

- `.NET`
- `MediatR`
- `SignalR`
- `EF Core`

### 3. Scoring and Monitoring Service

Responsabilidad:

- calcular puntajes
- aplicar penalizaciones
- mantener ranking derivado
- registrar historial auditable
- reaccionar a eventos del flujo operativo

Tecnologia esperada:

- `.NET`
- `MediatR`
- `EF Core`
- consumidor y publicador de `RabbitMQ`

### 4. Identity and Access Service

Responsabilidad:

- gestionar autenticacion
- gestionar autorizacion
- resolver roles y permisos
- exponer la capacidad de identidad al resto del sistema

Implementacion inicial:

- `Keycloak` como proveedor de identidad
- `JWT` como token consumido por los servicios

Nota:

- a nivel de arquitectura se habla de `Identity and Access Service`
- a nivel tecnologico se usa `Keycloak`

### 5. API Gateway o BFF

Responsabilidad sugerida:

- entrada unificada para clientes
- enrutamiento a microservicios
- simplificar auth para clientes
- centralizar preocupaciones de borde si hace falta
- nunca reemplazar la autorizacion propia de cada servicio

Estado:

- ya existe localmente como `edge-proxy`
- es la entrada publica preferida para clientes en desarrollo local
- sigue siendo un concern de borde; no reemplaza la validacion `JWT` ni la autorizacion propia de cada servicio

### 6. Web

Responsabilidad:

- interfaz para `Administrator` y `Operator`
- consumo de APIs
- consumo de tiempo real

### 7. Mobile

Responsabilidad:

- experiencia del `Participant`
- consumo de APIs
- consumo de tiempo real
- escaneo QR

### 8. RabbitMQ

Responsabilidad:

- desacoplar eventos secundarios
- soportar auditoria e historial
- permitir reaccion asincrona donde no haga falta respuesta inmediata

### 9. PostgreSQL

Responsabilidad:

- persistencia principal de los servicios

Decision pendiente de infraestructura:

- una BD fisica con esquemas separados por servicio
- o varias BDs fisicas, una por servicio

Para el inicio, si necesitan reducir complejidad operativa, pueden correr una sola instancia de `PostgreSQL` con separacion logica por servicio.

## Topologia logica

```mermaid
flowchart LR
    Web[Web App]
    Mobile[Mobile App]
    Gateway[API Gateway or BFF]
    Mission[Mission Design Service]
    Session[Session Operations Service]
    Score[Scoring and Monitoring Service]
    Identity[Identity and Access Service]
    KC[Keycloak]
    MQ[RabbitMQ]
    PG[(PostgreSQL)]

    Web --> Gateway
    Mobile --> Gateway
    Gateway --> Mission
    Gateway --> Session
    Gateway --> Score
    Gateway --> Identity
    Identity --> KC
    Session --> MQ
    Score --> MQ
    Mission --> PG
    Session --> PG
    Score --> PG
    Identity --> KC
```

## Vista local con `docker-compose`

```mermaid
flowchart TD
    subgraph DockerCompose
        GW[gateway optional]
        MS[mission-management-service]
        SS[session-management-service]
        SA[scoring-monitoring-service]
        KC[keycloak]
        DB[postgres]
        MQ[rabbitmq]
        W[web]
    end

    M[mobile local] --> GW
    W --> GW
    GW --> MS
    GW --> SS
    GW --> SA
    GW --> KC
    MS --> DB
    SS --> DB
    SA --> DB
    SS --> MQ
    SA --> MQ
    SS --> KC
    SA --> KC
    W --> KC
    M --> KC
```

## Comunicacion entre microservicios

### Sincrona

Usar para:

- consultas necesarias en linea
- acciones que requieren respuesta inmediata
- coordinacion principal entre servicios

Mecanismos posibles:

- REST
- gRPC en una fase posterior si hace falta

### Asincrona

Usar para:

- auditoria
- historial
- notificaciones secundarias
- eventos de dominio que no deban bloquear el flujo principal
- actualizaciones que no deben bloquear el flujo principal

Mecanismo:

- `RabbitMQ`

## Orquestacion vs coreografia

Las colas no son por si solas la orquestacion.

### Coreografia

Sirve cuando:

- un servicio publica un evento
- otros servicios reaccionan sin coordinador central fuerte

Ejemplo:

- `Session Operations Service` publica `EvidenceValidated`
- `Scoring and Monitoring Service` recalcula puntaje

### Orquestacion

Sirve cuando:

- un flujo de negocio requiere coordinacion explicita
- un servicio o proceso decide el siguiente paso

Ejemplo posible:

- `Session Operations Service` coordina flujo principal de sesion
- otros servicios reaccionan o responden

### Recomendacion para UMBRAL

- usar comunicacion sincronica como ruta principal entre microservicios
- usar `coreografia` solo para eventos secundarios y desacoplamiento
- mantener la autoridad del flujo principal en `Session Operations Service`
- evitar inventar una capa de orquestacion distribuida compleja desde el primer dia si no hay una necesidad real

## Responsabilidades por frontera

### Clientes <-> Gateway

- entrada unificada si se usa gateway
- autenticacion inicial
- simplificacion de endpoints para web y mobile
- propagacion del token hacia los servicios sin asumir autorizacion de negocio en el gateway

### Gateway <-> Microservicios

- enrutamiento
- composicion basica si hace falta
- no meter logica de dominio aqui
- no confiar en el gateway como unica frontera de validacion `JWT` o permisos

### Servicios <-> Identity and Access

- validacion de tokens
- autorizacion por roles y permisos
- separacion entre identidad y dominio central
- cada servicio sigue siendo responsable de validar `JWT` y aplicar sus reglas de autorizacion aunque el gateway sea la entrada publica

### Identity and Access <-> Keycloak

- `Keycloak` implementa la capacidad de identidad
- no debe confundirse la herramienta con el nombre del bounded context

### Servicios <-> PostgreSQL

- cada servicio debe tender a ser dueÃ±o de sus datos
- si comparten una sola instancia al inicio, deben seguir manteniendo separacion logica

### Servicios <-> RabbitMQ

- intercambio de eventos
- procesamiento asincrono
- desacoplamiento de responsabilidades

### Session Operations <-> Scoring and Monitoring

- preferir integracion sincronica si el flujo necesita respuesta inmediata
- usar eventos cuando la operacion pueda resolverse en segundo plano

## Target interno por servicio

> **Actualizado (superado el supuesto original).** Cada servicio ya está partido en **cuatro proyectos**
> (`*.Api` / `*.Application` / `*.Domain` / `*.Infrastructure`) más `tests/`, no en un único `.Api`. Ver
> [ADR-015](adr/ADR-015-framework-free-shared-kernel.md) y `repo-structure.md`. El target interno esperado
> por servicio es:

- `Presentation`
- `Application`
- `Domain`
- `Infrastructure`

Reglas:

- `Presentation` contiene endpoints, auth adapters y hubs de `SignalR`
- `Application` coordina casos de uso y no debe depender de `Infrastructure`
- `Infrastructure` implementa detalles tecnicos hacia afuera
- el codigo tecnico compartido entre servicios debe salir a `src/shared`, no quedarse como pseudo-contexto dentro de `src/services`

## Eventos asincronos a definir

Eventos minimos sugeridos:

- `MissionPublished`
- `EvidenceSubmitted`
- `EvidenceValidated`
- `PenaltyApplied`
- `SessionFinished`
- `AuditEntryRequested`

## Tiempo real

`SignalR` sigue siendo el mecanismo de tiempo real, pero concentrado principalmente en `Session Operations Service`.

Eventos de tiempo real a cerrar:

- cambio de estado de sesion
- liberacion de pistas
- actualizacion de ranking
- avance de etapa por equipo
- evidencia que requiere atencion del operador
- revelacion de soluciones al finalizar

## Variables de entorno sugeridas

### Mission Design Service

- `ConnectionStrings__Postgres`
- `Auth__Authority`
- `Auth__Audience`

### Session Operations Service

- `ConnectionStrings__Postgres`
- `RabbitMQ__Host`
- `RabbitMQ__User`
- `RabbitMQ__Password`
- `Auth__Authority`
- `Auth__Audience`
- `SignalR__Enabled`

### Scoring and Monitoring Service

- `ConnectionStrings__Postgres`
- `RabbitMQ__Host`
- `RabbitMQ__User`
- `RabbitMQ__Password`
- `Auth__Authority`
- `Auth__Audience`

### Identity and Access / Keycloak

- `KEYCLOAK_ADMIN`
- `KEYCLOAK_ADMIN_PASSWORD`
- realm, client ids y secrets

### Gateway

- urls internas de servicios
- configuracion de auth

### Web

- `VITE_GATEWAY_URL` o urls de servicios si no hay gateway
- `VITE_AUTH_URL`
- `VITE_SIGNALR_URL`

### Mobile

- `API_URL` o gateway
- `AUTH_URL`
- `SIGNALR_URL`

## Secuencia base de autenticacion

```mermaid
sequenceDiagram
    participant U as User
    participant C as Client
    participant I as Identity and Access
    participant K as Keycloak
    participant S as Service

    U->>C: inicia login
    C->>I: solicita autenticacion
    I->>K: delega autenticacion
    K-->>C: JWT
    C->>S: request con JWT
    S-->>C: respuesta autorizada
```

## Secuencia base de evento entre servicios

```mermaid
sequenceDiagram
    participant Client
    participant Session as Session Operations
    participant MQ as RabbitMQ
    participant Score as Scoring and Monitoring

    Client->>Session: command
    Session->>Session: aplica caso de uso
    Session->>MQ: publica evento
    MQ-->>Score: entrega evento
    Score->>Score: recalcula puntaje
```

## Secuencia base de tiempo real

```mermaid
sequenceDiagram
    participant O as Operator/Web
    participant Session as Session Operations
    participant RT as SignalR
    participant P as Participant/Mobile

    O->>Session: comando REST
    Session->>Session: aplica caso de uso
    Session->>RT: publica actualizacion
    RT-->>P: evento en tiempo real
    RT-->>O: confirmacion observable
```

## Camino de evolucion hacia nube

### Fase 1: local

- `docker-compose`
- contenedores por microservicio
- una instancia de `PostgreSQL`
- una instancia de `RabbitMQ`
- `Keycloak`

### Fase 2: pre-cloud

- healthchecks
- imagenes versionadas
- configuracion por ambiente
- pipelines de build y test
- contratos entre servicios mas estrictos

### Fase 3: cloud

- despliegue independiente por servicio
- secretos gestionados por plataforma
- observabilidad centralizada
- balanceo, TLS y dominio

## `CI/CD` en GitHub Actions

> **Actualizado: ya implementado (parcial).** El pipeline existe en `.github/workflows/validation.yml`
> (jobs `code-validation` y `compose-smoke`, más un lane rápido de unit tests) — ver `validation-pipeline.md`.
> Cubre restore/build, tests por servicio, checks de web y mobile, cobertura backend, validación de
> `docker-compose` y smoke de auth. **Aún NO** cubre build/push de imágenes a un registry ni despliegue por
> ambiente (fase cloud, pendiente).

Alcance objetivo del pipeline (✓ = ya implementado):

1. ✓ restore e install
2. ✓ build por microservicio
3. ✓ tests por microservicio
4. ✓ build web (y checks de mobile)
5. ✓ validacion basica de `docker-compose`
6. build de imagenes — pendiente
7. push a registry — pendiente
8. despliegue por ambiente — pendiente

## Decisiones ya asumidas por este bosquejo

- arquitectura de microservicios real desde el inicio
- `CQRS` logico con `MediatR`
- `SignalR` para tiempo real
- `Identity and Access` como capacidad logica
- `Keycloak` como implementacion inicial de identidad
- comunicacion sincronica como mecanismo principal entre servicios
- `RabbitMQ` para mensajeria asincrona secundaria
- `docker-compose` como entorno inicial local

## Preguntas que no bloquean el bosquejo, pero si la implementacion detallada

- si habra gateway desde el primer sprint o despues
- si cada servicio tendra base separada fisicamente o compartiran una instancia inicial
- que eventos seran solo internos y cuales tendran contratos estables
- que permisos finos se definiran ademas de roles
- que flujos requeriran orquestacion explicita y cuales funcionaran por coreografia
