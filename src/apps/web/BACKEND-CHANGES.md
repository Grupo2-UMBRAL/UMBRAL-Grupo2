# Cambios Pendientes en el Backend

Cambios que el backend necesita para completar la integración con el nuevo frontend SPA.

---

## 1. Keycloak — Cliente `umbral-web`

El flujo cambió de `grant_type: password` (BFF) a **Authorization Code Flow con PKCE** directo desde el navegador.

### Verificar/configurar:
- **Standard Flow Enabled**: `true`
- **Direct Access Grants Enabled**: puede deshabilitarse (ya no se usa)
- **Valid Redirect URIs**: incluir `http://localhost:3000/*` y `http://localhost:5173/*`
- **Web Origins**: incluir `http://localhost:3000` y `http://localhost:5173`
- **PKCE Code Challenge Method**: `S256`

> Si el cliente ya tiene Standard Flow habilitado y las URIs permitidas, no hay cambio requerido.

---

## 2. Edge Proxy — CORS

### Verificar:
- `AllowedOrigins` en la configuración de YARP debe incluir:
  - `http://localhost:3000`
  - `http://localhost:5173`

> El docker-compose.dev.yml ya tiene `AllowedOrigins__0: http://localhost:5173`. Verificar que `http://localhost:3000` también esté.

---

## 3. Docker Compose — Servicio `web`

### Cambios en `docker-compose.dev.yml`:

```diff
 web:
   build:
     context: ./src/apps/web
-  command: npm run dev -- --hostname 0.0.0.0
+  command: npm run dev
   environment:
-    - WATCHPACK_POLLING=true
     - NODE_ENV=development
     # Las demás variables se mantienen igual
```

El Dockerfile ya está ajustado para Vite. El `server.host: true` en vite.config.ts reemplaza `--hostname 0.0.0.0`.

---

## 4. Variables de Entorno

El frontend SPA usa las mismas variables de entorno que antes, inyectadas via `vite.config.ts define`:

| Variable | Uso |
|---|---|
| `NEXT_PUBLIC_EDGE_PROXY_BASE_URL` | URL pública del edge-proxy (default: `http://localhost:7500`) |
| `NEXT_PUBLIC_KEYCLOAK_BASE_URL` | URL pública de Keycloak (default: `http://localhost:7500/auth`) |
| `KEYCLOAK_REALM` | Realm de Keycloak (default: `umbral`) |
| `KEYCLOAK_WEB_CLIENT_ID` | Client ID (default: `umbral-web`) |
| `NEXT_PUBLIC_SESSION_OPERATIONS_HUB_PATH` | Path del hub SignalR de sesiones |
| `NEXT_PUBLIC_SCORING_AUDIT_HUB_PATH` | Path del hub SignalR de scoring |

> Los nombres `NEXT_PUBLIC_*` se mantienen por compatibilidad con el stack actual. Se pueden renombrar a `VITE_*` en un futuro refactor.

---

## 5. Endpoints que NO cambian

Todos los endpoints de API documentados en el plan de implementación siguen siendo consumidos de la misma forma. El frontend hace fetch directo al edge-proxy con `Authorization: Bearer <token>`.

No hay cambios requeridos en:
- identity-access-service
- mission-management-service
- session-operations-service
- scoring-audit-service
- edge-proxy routing rules

---

## 6. Archivos eliminados del frontend (ya no existen)

Estos archivos del Next.js original ya no son necesarios:
- `middleware.ts` — el enrutamiento es client-side
- `src/app/api/auth/login/route.ts` — el login es directo a Keycloak
- `src/app/api/auth/logout/route.ts` — el logout es directo a Keycloak
- `src/lib/session-cookie.ts` — ya no hay cookies de sesión
- `src/lib/session.ts` — reemplazado por auth-context con keycloak-js
- `next.config.ts` — reemplazado por vite.config.ts
