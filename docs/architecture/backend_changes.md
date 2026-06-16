# Cambios al Backend Faltantes

No se requieren cambios en los microservicios del backend ni en la configuración de infraestructura para soportar esta migración.

## Justificación y Análisis
1. **Configuración de Keycloak**: El cliente `umbral-web` definido en `infra/keycloak/import/umbral-realm.json` ya tiene habilitado `standardFlowEnabled: true` (flujo de código de autorización) y tiene registradas las URLs de redirección correctas:
   - `http://localhost:3000/*`
   - `http://localhost:5173/*`
   así como los orígenes permitidos en CORS (`webOrigins`).
2. **Proxy de Borde (Edge Proxy)**: `AllowedOrigins` en `docker-compose.dev.yml` y YARP ya incluye `http://localhost:3000` y `http://localhost:5173`, lo cual permite solicitudes CORS desde la SPA al proxy de borde.
3. **Manejo de Tokens**: La SPA envía el token `Bearer <accessToken>` decodificado client-side en las cabeceras HTTP hacia el proxy de la misma forma que el Next.js anterior, de modo que los microservicios autentican y autorizan de forma idéntica.
