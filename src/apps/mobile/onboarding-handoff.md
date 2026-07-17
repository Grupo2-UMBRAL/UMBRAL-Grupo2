# Handoff — Tutorial de entrada + mascota lupa

> Estado: **implementado; animaciones verificadas por medición en el DOM (§4); sin cobertura
> Playwright.** Queda sin verificar el flujo del flag (§4.5), que necesita login real.
> Lo que sigue está pensado para que otra persona pueda terminarlo sin reconstruir el contexto.

## 1. Qué se construyó

Entrada para el participante nuevo, en dos piezas que **no compiten**:

- **Tutorial (`OnboardingTutorial`)** — el evento de primera vez. Modal a pantalla completa, 4
  pasos, skippeable. Dispara **al entrar a home**.
- **Checklist (`OnboardingChecklist`)** — el mueble permanente. Pill abajo que abre una tarjeta
  plegable. No interrumpe; queda hasta que se descarta.

Además, la **mascota pasó a ser la chispa sosteniendo una lupa**, guiñando el ojo del cristal e
inclinándose al guiñar.

## 2. Alcance exacto del cambio

| Archivo | Qué |
| --- | --- |
| `src/components/onboarding.tsx` | **Nuevo.** Los 4 pasos + tutorial + checklist. |
| `src/components/mascot.tsx` | **Reescrito.** Misma API (`mood`, `size`) → ningún call-site cambió. |
| `src/lib/session-storage.ts` | `loadOnboardingState` / `saveOnboardingState` + clave `umbral.mobile.onboarding`. |
| `app/mobile/(participant)/home.tsx` | Monta ambas piezas y lee/escribe el flag. |

**No son parte de este cambio** (son trabajo en curso de otra persona, presentes en el árbol):
`src/apps/mobile/README.md` y `src/apps/mobile/package.json` (túnel de Expo Go), `main.bicep`,
`main.json`, `docker-compose.demo.yml`, `docker-compose.dev.yml`.

`mascot.tsx` lo consumen **7 sitios**: `board.tsx` (celebrate y sad), `ranking.tsx`,
`forbidden.tsx`, `signup.tsx`, `answer-feedback.tsx`, `session-lobby.tsx`. Ninguno se tocó.

## 3. Lo que SÍ está verificado (y cómo)

```
cd src/apps/mobile
npm run typecheck        # limpio
npx jest --runInBand     # 25/26 — ver §5
```

- `tsc --noEmit` limpio.
- El bundle web compila (`expo export` y el dev server).
- `board.test.tsx` pasa, y renderiza la mascota nueva en `celebrate` y `sad`.
- Geometría de la mascota **medida en el DOM** (no calculada) en `/mobile/signup`, que la renderiza
  a 104dp y no pide login: 8 formas, giro −8°, lupa a −23.3 y ojo a +16 del centro del cuerpo
  (simétricos salvo el desfase deliberado, ver §6), todo dentro de la caja (8.9→98.7 en X,
  5.9→88.1 en Y).

## 4. Verificación de las animaciones — hecha por medición, no por vista

El panel de navegador sigue permanentemente en `visibilityState: "hidden"`: `requestAnimationFrame`
dispara **0 veces** y los screenshots se cuelgan. Eso ya no bloquea.

**El truco:** `setTimeout` **no** está estrangulado en ese panel (medido: ~4.6ms por tick). Basta
sustituir `requestAnimationFrame` por un shim sobre `setTimeout` y las animaciones corren de verdad
— mismo código `Animated`, mismos timestamps, misma interpolación; sólo cambia la *fuente de
frames*. Como el bucle del guiño se queda congelado en su primer `rAF`, hay que **remontar** el
componente después de instalar el shim (navegación client-side con `pushState` + `popstate`; un
reload borraría el shim).

```js
window.requestAnimationFrame = (cb) => setTimeout(() => cb(performance.now()), 16);
window.cancelAnimationFrame = (id) => clearTimeout(id);
// …instalar, luego remontar, luego medir estilos inline.
```

Ojo al medir: **no selecciones por tamaño medido**. El ojo del cuerpo (14.56px) rotado a −18° tiene
una caja alineada a ejes de ~18.3px y se confunde con el ojo de la lupa (18.72px). Ancla por
estructura — la lupa es el único `div` con `borderWidth ≈ size*0.05`, y el ojo es su único hijo.

Medido así, contra `size = 104`:

| § | Qué | Esperado | Medido | |
| --- | --- | --- | --- | --- |
| 4.1 | Guiño, alto del ojo | 18.72 → 3.00 (`×0.16`) | 18.72 → **3.00**, 40 valores distintos | ✅ |
| 4.1 | Fases del guiño | 220 / 200 / 300 ms | **203 / 215 / 293** ms | ✅ |
| 4.1 | Ciclo | 3120 ms | ~3202 ms | ✅ |
| 4.2 | Inclinación | −8° → −18° | **−8° → −18°**, nunca lo pasa | ✅ |
| 4.3 | Transición de página | opacidad 1→0→1; sale 140, entra 240 | 0 a los ~150ms, 1 a los ~388ms | ✅ |
| 4.3 | Dirección | sale a −28, entra desde +28 | sale a −25.65, salta a **+28**, vuelve a 0 | ✅ |
| 4.4 | Punto activo | 10 → 28dp, gris → verde, 260ms | 10 → 28 (16 pasos), 15 colores, ~236ms | ✅ |

Las desviaciones (203 vs 220, ~236 vs 260) son del muestreo + la granularidad de 16ms del shim y del
umbral de detección contra un `Easing.out` asintótico, no del código.

**Los cuatro `mood`, medidos a la vez** — confirma la decisión de §6:

| `mood` | Alto del ojo | ¿Guiña? |
| --- | --- | --- |
| `happy` | 3.00 → 18.72 | sí ✅ |
| `celebrate` | 3.00 → 18.72 | sí ✅ |
| `waiting` | fijo 5.20 (`=104*0.05`) | no ✅ |
| `sad` | fijo 18.72 | no ✅ |

También confirmado en el DOM: el ojo guiña con `borderRadius` **fijo** en 18.72px mientras el alto
cambia — círculo → pill, nunca `scaleY` (§12). Y el reposo es exactamente `matrix(0.990268,
−0.139173, …)` = −8°, con el mango a +25°.

### 4.5 — Lo único que sigue sin verificar

**El flujo completo del flag**: que el tutorial salga una sola vez, que "Saltar" persista, que el
checklist aparezca sólo tras cerrar el tutorial. No se pudo medir en el navegador: vive en `home`,
que está detrás de `useSession` / `useTeamSnapshot`, y montarlo suelto verificaría el arnés y no
`home.tsx`. Necesita **login real (stack docker)** o un test que mockee la sesión al estilo de
`board.test.tsx`. La lógica de `home.tsx` se revisó a mano y es la correcta (`null` → nada;
`tutorialSeen === false` → tutorial; `tutorialSeen && !checklistDismissed` → checklist).

### 4.6 — La mascota a 24dp: hallazgo real

Medida a 24dp, **el guiño desaparece**: el ojo cierra a **0.69px** (`24 × 0.18 × 0.16`), por debajo
de un píxel físico a 1× DPR. No se lee como guiño; a lo sumo un parpadeo de antialias. La animación
corre igual (38 valores distintos), así que se paga el coste sin obtener el gesto.

Sigue sin haber **ninguna pantalla que la use a ese tamaño**, así que es riesgo futuro, no un bug
abierto — pero si alguien mete la mascota en un glifo <40dp, el `×0.16` necesita un mínimo en px.

Cómo reproducir la medición (o probar a ojo en una máquina con navegador visible):

```
cd src/apps/mobile
npm run env:development
npx expo start --web --port 19006
```

El puerto **tiene que ser 19006**: las allowlists de CORS del edge-proxy
(`docker-compose.dev.yml`, `AllowedOrigins`) y del dashboard de Aspire sólo permiten
`3000 / 5173 / 19006`. En 8081 (el default de expo) el login falla por CORS.

Para volver a ver el tutorial desde cero hay que borrar la clave `umbral.mobile.onboarding` de
AsyncStorage (en web: `localStorage`).

## 5. Problema conocido, ajeno a este cambio

`ranking.test.tsx › loads ranking and highlights the participant team` **falla**. Se comprobó
corriéndolo aislado contra `HEAD` limpio: **falla idéntico sin estos cambios**. No es un flake —
falla de forma determinista.

Relevante porque `ranking.tsx` renderiza la mascota, así que era candidato legítimo a regresión.
No lo es.

**Causa raíz encontrada (y el roto es el test, no el producto).** `ranking.tsx` funciona. El
`waitFor` de la línea 126 espera `"Clasificación en vivo"`, que es el **título del `ScreenShell`** y
está desde el primer render con enrollment hidratado — **antes** de que resuelvan los dos `fetch`.
La línea 130 corre síncrona contra un ranking que aún no llegó y pega contra el estado vacío. El
segundo test pasa porque siembra las filas por SignalR dentro de `await act(async …)`, que sí vacía
la cola de promesas.

El fix es esperar el dato en vez del chrome, y deja la suite 2/2 en verde (verificado):

```diff
-  await waitFor(() => { expect(screen.getByText("Clasificación en vivo")).toBeTruthy(); });
-  expect(screen.getByText(/puesto #1 con 200 pts/i)).toBeTruthy();
+  await waitFor(() => { expect(screen.getByText(/puesto #1 con 200 pts/i)).toBeTruthy(); });
```

**No va en esta rama** (`AGENTS.md` §2: un cambio por rama); merece su propio commit. El patrón
—esperar el título en vez del dato— conviene revisarlo en los demás `waitFor` antes de escribir los
de Playwright.

## 6. Decisiones ya tomadas — no re-litigar sin motivo

- **El tutorial dispara al entrar a home, no al unirse a un equipo.** Consecuencia asumida: el
  participante nuevo llega sin sesión, así que el tutorial cuenta la dinámica antes de que exista
  una etapa donde colgarla. Se acepta a cambio de que ocurra sí o sí, en vez de depender de que el
  jugador llegue a inscribirse.
- **El flag no se borra al cerrar sesión ni al cambiar de sesión de juego.** Haber entendido la
  dinámica es propiedad de la persona, no de la partida.
- **Hay un tercer estado `null` mientras se lee AsyncStorage.** Sin él, el tutorial parpadea en
  cada entrada de quien ya lo vio, porque `false` se renderiza antes de saber la respuesta.
- **El guiño sólo corre en `happy` y `celebrate`.** `waiting` ya tiene los dos ojos cerrados, y un
  guiño alegre sobre `sad` contradice lo que la pantalla dice (`sad` sale en `board` al fallar y en
  `forbidden`).
- **El guiño anima `height` con `borderRadius` fijo, nunca `scaleY`.** Aplastar un círculo produce
  un óvalo y §12 los prohíbe. Círculo → pill es el mismo idioma con el que la mascota ya dibujaba
  `waiting`.
- **−18° es el techo de la inclinación.** Medido: a 24dp la cabeza toca exactamente el borde
  superior de su caja. Más giro exige encoger el cuerpo ~4%.
- **La lupa está a −23 y el ojo a +16 del centro: la asimetría es deliberada** (`LENS_EXTRA_LEFT`),
  para que la lente asome del cuerpo y se lea como herramienta y no como monóculo. Subirla
  reproduce un descuadre que ya se corrigió una vez.
- **La tarjeta del checklist va despegada del borde inferior.** Pegada abajo tapaba el "Cerrar
  sesión" de home, que es el último elemento del scroll.
- **Toda la profundidad es labio inferior sólido, nunca sombra difusa** (§3).
- **`useNativeDriver: false`** en todo, siguiendo la convención que ya usan `answer-feedback.tsx` y
  `session-lobby.tsx`.

## 7. Alternativas descartadas — este documento es el único registro

Se exploraron con `/prototype` y **se borraron sin commitear por decisión explícita**. No existen
en ninguna rama ni en el historial. Se documentan aquí para que nadie las reproponga sin saber por
qué cayeron:

- **Coach marks contextuales** (señalar elementos reales de home con scrim + burbuja). Descartada:
  se queda sin anclajes justo en el estado que tiene el 100% de los usuarios nuevos — sin sesión,
  home sólo muestra el hero "Aún no estás en una sesión" y los tiles ni existen en el árbol.
- **Lupa-cara** (la lente es la cabeza) y **Lupa-ojo** (la lente es el cuerpo con un ojo enorme).
  Descartadas: ambas **reemplazan** al personaje. La ganadora lo conserva y sólo le suma un gesto,
  que es lo que sostiene la continuidad con las 7 pantallas donde ya vive.

## 8. Pendiente para quien lo tome

1. **Verificar §4.5** (el flujo del flag). Es lo único de §4 que queda; necesita login real o un
   test con la sesión mockeada. Todo lo demás de §4 ya está medido.
2. **Cobertura Playwright.** `AGENTS.md` dice *"Frontend components will be tested via Playwright"*.
   Esto es código de producción sin ella. Merece test: que el tutorial se muestre una sola vez, que
   "Saltar" persista, que el checklist espere al cierre del modal.
3. **Higiene de rama.** `AGENTS.md` §2 dice *"One Change per Worktree"*. Esto está en `develop`
   mezclado con trabajo ajeno sin commitear (§2 de este doc). Hay que sacarlo a su propia rama
   antes de commitear nada.
4. **Coste de la animación.** El guiño corre en bucle con `useNativeDriver: false` (hilo de JS) y
   ahora vive en 7 pantallas. A esta escala probablemente da igual, pero no se midió — y es la clase
   de cosa que no se nota en desarrollo y sí en un teléfono de gama baja a mitad de una misión.
5. **Decisión abierta:** el checklist hoy desaparece para siempre al pulsar "No volver a mostrar".
   Nunca se decidió si debería poder recuperarse desde algún sitio.
