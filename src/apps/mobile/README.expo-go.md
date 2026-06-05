# Expo Go

Prueba el shell mobile en tu teléfono con `Expo Go`.

## 1. Instalar

- Android: instala `Expo Go` desde Play Store.
- iPhone: instala `Expo Go` desde App Store.

## 2. Variables que debes cambiar

Copia `src/apps/mobile/.env.example` a `src/apps/mobile/.env` y reemplaza `localhost` por la IP LAN de tu PC.

Ejemplo:

```env
EXPO_PUBLIC_EDGE_PROXY_BASE_URL=http://192.168.1.25:7000
EXPO_PUBLIC_KEYCLOAK_BASE_URL=http://192.168.1.25:7000/auth
EXPO_PUBLIC_KEYCLOAK_REALM=umbral
EXPO_PUBLIC_KEYCLOAK_CLIENT_ID=umbral-mobile
EXPO_PUBLIC_SESSION_OPERATIONS_HUB_PATH=/session-hub/hubs/session
```

La IP la sacas con:

```powershell
ipconfig
```

Usa la IPv4 de tu Wi-Fi. El teléfono y la PC deben estar en la misma red.

## 3. Levantar backend

Desde la raiz del repo:

```powershell
docker compose --env-file .env.example -f docker-compose.dev.yml up -d edge-proxy
```

## 4. Levantar Expo para el teléfono

Desde `src/apps/mobile`:

```powershell
npm install
npm run start:expo-go
```

## 5. Abrir en el teléfono

- Abre `Expo Go`.
- Escanea el QR que muestra Expo en tu terminal.

## 6. Qué probar

- `participant / participant123!` entra.
- `operator / operator123!` se rechaza por rol.
- `admin / admin123!` se rechaza por rol.
- el probe de `/health` responde `200`.

## Nota

Para `Expo Go` no uses `localhost` en las variables del mobile. Siempre usa la IP LAN de tu PC.
