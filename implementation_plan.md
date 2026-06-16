# Plan de Migración: Next.js a Vite + React SPA con Autenticación Directa Keycloak y Landing Page

Este documento define el plan técnico para migrar el frontend de UMBRAL de Next.js a una SPA (Single Page Application) basada en Vite + React. El desarrollo se iniciará desde cero y se delegará a un agente especializado.

---

## Propósito y Alcance

1. **Reemplazo Completo**: Eliminar el proyecto Next.js en [src/apps/web](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/apps/web) y recrearlo como SPA usando **Vite**, **React 19**, **TypeScript** y **React Router**.
2. **Landing Page Premium**: Crear una landing page en el root `/` con diseño visual inmersivo, tipografía moderna, animaciones dinámicas y tema oscuro ("glassmorphism").
3. **Login Directo en Keycloak**: Cambiar el flujo de login de credenciales directas (`grant_type: password` en backend) a redirección de OIDC (Authorization Code Flow con PKCE) hacia la interfaz oficial de Keycloak.
4. **Preservar Funcionalidades**: Migrar y adaptar el espacio de trabajo del Administrador (CRUD de misiones, gestión de operadores) y el espacio del Operador (monitoreo de LiveSessions, SignalR, puntuación).

---

## User Review Required

> [!IMPORTANT]
> **Cambio de Puerto de Desarrollo**: Por defecto, Vite inicia en `http://localhost:5173`. El `edge-proxy` ya tiene permitido este origen (`AllowedOrigins__0: http://localhost:5173`). No obstante, en Docker Compose el puerto mapeado es `3000`. Mantendremos Vite configurado para escuchar en el puerto `3000` (`server.port: 3000`) para no alterar los mapeos de red del contenedor ni variables de entorno del stack actual.

> [!WARNING]
> **Flujo OIDC y Proxy Reverso**: Al redirigir a Keycloak, el navegador del usuario debe poder acceder a la URL pública de Keycloak. En el entorno local, se expone a través de `http://localhost:7500/auth`. Asegurarse de que el frontend redirija a esta dirección de forma transparente y vuelva al SPA en `/` (o `/callback`) con el código de autorización.

---

## Open Questions

> [!NOTE]
> No hay preguntas pendientes críticas. Se asume que el backend actual de Keycloak ya tiene el cliente `umbral-web` configurado para permitir `Standard Flow` (Authorization Code Flow) y URLs de redirección válidas (por ejemplo, `http://localhost:3000/*` o `http://localhost:5173/*`).

---

## Proposed Changes

### 1. Estructura y Configuración del Proyecto Vite

#### [NEW] [vite.config.ts](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/apps/web/vite.config.ts)
- Configurar el servidor de desarrollo en puerto `3000` con `host: true` (para Docker).
- Configurar alias `@` apuntando a `src/`.

#### [MODIFY] [package.json](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/apps/web/package.json)
- Reemplazar scripts (`dev: "vite"`, `build: "tsc && vite build"`, `preview: "vite preview"`).
- Instalar dependencias clave: `react-router-dom`, `keycloak-js` (o `react-oidc-context`), `@microsoft/signalr`.
- Mantener las tipografías `@fontsource/ibm-plex-sans` e `ibm-plex-mono`.

---

### 2. Flujo de Autenticación OIDC Client-Side

#### [NEW] [auth-context.tsx](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/apps/web/src/context/auth-context.tsx)
- Inicializar `keycloak-js` con la configuración pública:
  - `url`: `NEXT_PUBLIC_KEYCLOAK_BASE_URL` (vía variables de entorno)
  - `realm`: `KEYCLOAK_REALM`
  - `clientId`: `KEYCLOAK_WEB_CLIENT_ID`
- Ejecutar inicialización de OIDC con `onLoad: 'check-sso'` y `pkceMethod: 'S256'`.
- Proveer:
  - `isAuthenticated`: boolean.
  - `token`: accessToken para headers de API.
  - `roles`: decodificados del JWT (claims directos o `realm_access.roles` filtrando por `Administrator`, `Operator`).
  - `user`: username y displayName.
  - `login()`: redirige a la UI de Keycloak.
  - `logout()`: redirige al logout de Keycloak.

---

### 3. Rutas y Vistas

#### [NEW] [LandingPage.tsx](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/apps/web/src/pages/LandingPage.tsx)
- Vista pública principal `/` con estilo premium futurista/industrial:
  - Fondo oscuro con sutiles gradientes de color violeta y azul profundo.
  - Efectos de "glassmorphism" en tarjetas y menús.
  - Tipografía imponente (`IBM Plex Sans` y `IBM Plex Mono` para datos técnicos).
  - Micro-animaciones en botones e interacciones.
  - Botón destacado de **"Ingresar a Consola"** que dispara `auth.login()`.

#### [NEW] [DashboardRouter.tsx](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/apps/web/src/pages/DashboardRouter.tsx)
- Ruta `/dashboard` protegida. Evalúa los roles del token decodificado:
  - Si es `Administrator` -> Redirige a `/administrator`.
  - Si es `Operator` -> Redirige a `/operator`.
  - En otro caso -> Redirige a `/forbidden`.

#### [NEW] [AdministratorPage.tsx](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/apps/web/src/pages/AdministratorPage.tsx) y [OperatorPage.tsx](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/apps/web/src/pages/OperatorPage.tsx)
- Recreación SPA de los workspaces. Inyectan el `token` del contexto de autenticación en las llamadas HTTP hacia `edge-proxy`.
- Adaptar layouts y SignalR a Hooks estándar de React 19.

---

### 4. Dockerización

#### [MODIFY] [Dockerfile](file:///c:/Users/sebav/OneDrive/Documentos/Proyectos%20desarrollo/UMBRAL-Grupo2/src/apps/web/Dockerfile)
- Ajustar para exponer el puerto `3000` y servir con Vite (o de forma dinámica en desarrollo).

---

### 5. API Contracts

El `edge-proxy` (puerto `7500` local / YARP en puerto `8080` interno) expone los siguientes prefijos de ruta que se mapean y limpian (stripping) antes de enviarse a cada microservicio:

| Prefijo de Ruta en Frontend | Destino de Servicio Interno | Remoción de Prefijo | Propósito |
| :--- | :--- | :--- | :--- |
| `/identity-access/*` | `http://identity-access-service:8080/*` | Sí (`/identity-access`) | Fachada de Operadores y claves |
| `/mission-design/*` | `http://mission-design-service:8080/*` | Sí (`/mission-design`) | CRUD e información de misiones |
| `/session-operations/*` | `http://session-operations-service:8080/*` | Sí (`/session-operations`) | Creación y control de sesiones activas |
| `/session-hub/*` | `http://session-operations-service:8080/*` | Sí (`/session-hub`) | Canal de eventos de sesión (SignalR) |
| `/scoring-audit/*` | `http://scoring-audit-service:8080/*` | Sí (`/scoring-audit`) | Rankings e historial de auditoría |
| `/auth/*` | `http://keycloak:8080/*` | Sí (`/auth`) | Proveedor de identidad Keycloak |

Todas las solicitudes deben incluir la cabecera `Authorization: Bearer <accessToken>` obtenida del flujo OIDC.

#### A. Gestión de Operadores (Identity & Access)
* **Base URL**: `{EDGE_PROXY_URL}/identity-access/api/identity-access/operators`
* **Listar Operadores**: `GET /`
  - *Response*: Array de objetos conteniendo: `id`, `username`, `displayName`, `firstName`, `lastName`, `email`, `isActive`, `roles`, `lastUpdatedAt`.
* **Crear Operador**: `POST /`
  - *Body (JSON)*:
    ```json
    {
      "username": "string",
      "firstName": "string",
      "lastName": "string",
      "email": "string",
      "password": "string"
    }
    ```
* **Desactivar Operador**: `POST /{userId}/deactivate`
* **Rotar Contraseña**: `POST /{userId}/reset-password`
  - *Body (JSON)*: `{ "password": "string" }`

#### B. Diseño de Misiones (Mission Design)
* **Base URL**: `{EDGE_PROXY_URL}/mission-design/api/mission-design/missions`
* **Listar Misiones**: `GET /`
* **Detalle de Misión**: `GET /{missionId}`
* **Crear Misión**: `POST /`
* **Actualizar Misión**: `PUT /{missionId}`
  - *Body (JSON) para Crear/Actualizar*:
    ```json
    {
      "name": "string",
      "description": "string",
      "difficulty": "Easy | Medium | Hard",
      "maximumDurationMinutes": number,
      "gameType": "string",
      "nodes": [
        {
          "id": "string | null",
          "name": "string",
          "order": number,
          "isActive": boolean,
          "defaultTimeBudgetMinutes": number | null,
          "timeBudgetMinutes": number | null,
          "difficulty": "Easy | Medium | Hard | null",
          "gameType": "Trivia | Treasure Hunt | null",
          "prompt": "string | null",
          "expectedQrHash": "string | null",
          "triviaValidAnswer": "string | null",
          "triviaInitialValidationCriterion": "string | null",
          "hints": [
            {
              "id": "string | null",
              "content": "string",
              "isSolution": boolean,
              "latitude": number | null,
              "longitude": number | null
            }
          ],
          "children": []
        }
      ]
    }
    ```
* **Desactivar Misión**: `POST /{missionId}/deactivate`
* **Misiones Elegibles para Sesión**: `GET /eligible-for-live-session`

#### C. Operaciones en Vivo (Session Operations & Scoring Audit)
* **Base URL**: `{EDGE_PROXY_URL}/session-operations/api/session-operations`
* **Listar Live Sessions**: `GET /live-sessions`
* **Detalle de Live Session**: `GET /live-sessions/{liveSessionId}/overview`
* **Detalle de Equipo**: `GET /live-sessions/{liveSessionId}/teams/{sessionTeamId}/detail?inactivityThresholdMinutes={minutes}`
* **Programar Live Session**: `POST /live-sessions`
  - *Body (JSON)*:
    ```json
    {
      "missionId": "string",
      "name": "string",
      "scheduledStartAtUtc": "string (ISO-8601)",
      "selectedMissionStageIds": ["string"]
    }
    ```
* **Control de Ciclo de Vida**: `POST /live-sessions/{liveSessionId}/lifecycle/{start | pause | resume | finalize | cancel}`
* **Desactivar Etapa en Vivo**: `POST /live-sessions/{liveSessionId}/stages/{missionStageId}/deactivate`
* **Aplicar Penalización**: `POST /live-sessions/{liveSessionId}/penalties`
  - *Body (JSON)*:
    ```json
    {
      "sessionTeamId": "string",
      "commandId": "string",
      "severity": "Low | Medium | High",
      "reason": "string"
    }
    ```
* **Liberar Pista**: `POST /live-sessions/{liveSessionId}/hints/{hintId}/release`
  - *Body (JSON)*: `{ "sessionTeamId": "string | null" }`
* **Crear Pista Operativa**: `POST /live-sessions/{liveSessionId}/stages/{stageId}/hints`
  - *Body (JSON)*:
    ```json
    {
      "content": "string",
      "latitude": number | null,
      "longitude": number | null
    }
    ```
* **Anulación de Evidencia (Override)**: `POST /submissions/{submissionId}/override`
  - *Body (JSON)*:
    ```json
    {
      "isAccepted": true,
      "reason": "string"
    }
    ```

#### D. Puntuación e Historial (Scoring Audit)
* **Base URL**: `{EDGE_PROXY_URL}/scoring-audit/api/scoring-audit/sessions/{liveSessionId}`
* **Listar Ranking**: `GET /ranking`
* **Registro de Auditoría**: `GET /event-log`

#### E. Flujos en Tiempo Real (SignalR)
* **Session Hub**: `{EDGE_PROXY_URL}/session-hub/hubs/session`
  - *Mensajes*: Escucha `liveSessionStateChanged` (payload: `LiveSessionStateChangedEvent`), `teamProgressChanged`, etc.
* **Scoring Hub**: `{EDGE_PROXY_URL}/scoring-audit/hub/scoring`

---

## Verification Plan

### Manual Verification
1. Levantar el stack mediante `docker compose up -d web edge-proxy keycloak postgres`.
2. Acceder a `http://localhost:3000/`. Validar estética premium de la Landing Page.
3. Presionar "Ingresar a Consola". Verificar redirección a la página de login oficial de Keycloak.
4. Autenticarse usando:
   - `admin / admin123!` -> Validar redirección a `/administrator`.
   - `operator / operator123!` -> Validar redirección a `/operator`.
5. Probar CRUDs de misiones, creación de operadores y sincronización en tiempo real de SignalR.
