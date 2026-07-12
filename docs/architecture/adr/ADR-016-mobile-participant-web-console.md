# ADR-016: Móvil como cliente del `Participant`; web como consola de `Administrator` / `Operator`

## Status

Accepted

## Context

El enunciado académico plantea una tensión que hay que resolver explícitamente:

- Su objetivo general habla de *"desarrollar una **aplicación web**"* y su cláusula de alcance excluye
  *"aplicaciones móviles nativas"* y la *"geolocalización precisa de participantes"*.
- Al mismo tiempo, el `must` tecnológico es **React** en el frontend, y la experiencia del participante
  (jugar la misión: recibir pistas, escanear QR, responder trivia, ver su temporizador y puntaje) encaja
  mucho mejor en un dispositivo móvil en mano que en una consola de escritorio.

El equipo decidió repartir la superficie de cliente y esta división **ya está fija**. Sin un ADR, un lector
del enunciado puede leer `src/apps/mobile` como una violación de la exclusión de "apps móviles nativas".

## Decision

Se fija la siguiente división de clientes, sin reabrir:

1. **El juego en sí (experiencia del `Participant`) vive en `src/apps/mobile`** — Expo / React Native 0.81 /
   `expo-router`. Es el cliente que un jugador usa durante la sesión en vivo.
2. **La consola de `Administrator` y `Operator` vive en `src/apps/web`** — React 19 + Vite + TS. Cubre
   autoría de misiones (Admin), gestión de usuarios operadores (Admin) y operación de sesiones en vivo
   (Operator): liberar pistas, penalizar, observar ranking.

### Por qué esto no contradice la exclusión del enunciado

- *"Aplicación móvil nativa"* se interpreta como un binario compilado por plataforma con toolchain nativa
  (Swift/Kotlin, Android SDK / Xcode). **Expo / React Native no es eso**: es una sola base JavaScript
  cross-platform, alineada con el `must` de React, y de hecho puede correr como web (`:19006` en el stack
  de desarrollo). No introduce una tecnología fuera de las bases del enunciado; extiende React al dispositivo.
- El `must` "frontend en React" queda **doblemente satisfecho**: web es React, móvil es React Native.

### Lo que sigue fuera de alcance (no lo abre este ADR)

- **Geolocalización precisa / tracking en vivo del participante.** El móvil usa solo un **mapa estático**
  con pines de pistas colocados por el `Administrator` en autoría; no rastrea la posición del jugador.
- Cobros en línea, IA aplicada al contenido de misiones, integración con dispositivos físicos.

## Consequences

Positivas:

- La demostración del flujo principal se reparte de forma natural: autoría y operación en la consola web;
  registro de equipo, recepción de pista, envío de evidencia y ranking en el móvil del participante.
- El QR (escaneo de evidencia de búsqueda) tiene un hogar natural en el cliente móvil.

Coste:

- Dos toolchains de frontend conviven en `docker-compose` y CI (`Vite` para web, `Expo`/Metro para móvil).
  El job de CI ya contempla ambos (lint/typecheck/build de web y typecheck/build de móvil).
- La superficie de UI a mantener y a presentar en la defensa es mayor que una sola app web.

## Related

- `docs/product/ers-umbral.md` — RF de participación móvil (QR, mapa estático).
- `docs/product/design.md` — web = calma/operacional; móvil = Duolingo-bright.
- `src/apps/mobile/src/theme/tokens.ts` — paleta móvil (referencia de alineación del web).
- `docs/architecture/repo-structure.md` — ubicación de `src/apps/web` y `src/apps/mobile`.
