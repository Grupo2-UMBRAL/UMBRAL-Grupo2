# Cambios Pendientes en el Backend

Cambios que el backend necesita para completar la integraciÃ³n con el nuevo frontend SPA.

---

## 1. Keycloak â€” Cliente `umbral-web`

El flujo cambiÃ³ de `grant_type: password` (BFF) a **Authorization Code Flow con PKCE** directo desde el navegador.

### Verificar/configurar:
- **Standard Flow Enabled**: `true`
- **Direct Access Grants Enabled**: puede deshabilitarse (ya no se usa)
- **Valid Redirect URIs**: incluir `http://localhost:3000/*` y `http://localhost:5173/*`
- **Web Origins**: incluir `http://localhost:3000` y `http://localhost:5173`
- **PKCE Code Challenge Method**: `S256`

> Si el cliente ya tiene Standard Flow habilitado y las URIs permitidas, no hay cambio requerido.

---

## 2. Edge Proxy â€” CORS

### Verificar:
- `AllowedOrigins` en la configuraciÃ³n de YARP debe incluir:
  - `http://localhost:3000`
  - `http://localhost:5173`

> El docker-compose.dev.yml ya tiene `AllowedOrigins__0: http://localhost:5173`. Verificar que `http://localhost:3000` tambiÃ©n estÃ©.

---

## 3. Docker Compose â€” Servicio `web`

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
     # Las demÃ¡s variables se mantienen igual
```

El Dockerfile ya estÃ¡ ajustado para Vite. El `server.host: true` en vite.config.ts reemplaza `--hostname 0.0.0.0`.

---

## 4. Variables de Entorno

El frontend SPA usa las mismas variables de entorno que antes, inyectadas via `vite.config.ts define`:

| Variable | Uso |
|---|---|
| `NEXT_PUBLIC_EDGE_PROXY_BASE_URL` | URL pÃºblica del edge-proxy (default: `http://localhost:7500`) |
| `NEXT_PUBLIC_KEYCLOAK_BASE_URL` | URL pÃºblica de Keycloak (default: `http://localhost:7500/auth`) |
| `KEYCLOAK_REALM` | Realm de Keycloak (default: `umbral`) |
| `KEYCLOAK_WEB_CLIENT_ID` | Client ID (default: `umbral-web`) |
| `NEXT_PUBLIC_SESSION_OPERATIONS_HUB_PATH` | Path del hub SignalR de sesiones |
| `NEXT_PUBLIC_SCORING_MONITORING_HUB_PATH` | Path del hub SignalR de scoring |

> Los nombres `NEXT_PUBLIC_*` se mantienen por compatibilidad con el stack actual. Se pueden renombrar a `VITE_*` en un futuro refactor.

---

## 5. Endpoints que NO cambian

Todos los endpoints de API documentados en el plan de implementaciÃ³n siguen siendo consumidos de la misma forma. El frontend hace fetch directo al edge-proxy con `Authorization: Bearer <token>`.

No hay cambios requeridos en:
- identity-access-service
- mission-management-service
- session-operations-service
- scoring-monitoring-service
- edge-proxy routing rules

---

## 6. Archivos eliminados del frontend (ya no existen)

Estos archivos del Next.js original ya no son necesarios:
- `middleware.ts` â€” el enrutamiento es client-side
- `src/app/api/auth/login/route.ts` â€” el login es directo a Keycloak
- `src/app/api/auth/logout/route.ts` â€” el logout es directo a Keycloak
- `src/lib/session-cookie.ts` â€” ya no hay cookies de sesiÃ³n
- `src/lib/session.ts` â€” reemplazado por auth-context con keycloak-js
- `next.config.ts` â€” reemplazado por vite.config.ts
