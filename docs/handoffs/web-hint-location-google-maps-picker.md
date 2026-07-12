# Handoff — Google Maps location picker for hint authoring (web console)

**Goal:** In the Admin mission builder, let an author pick a hint's location on a
Google Map with a **Places search box** (type an address / place, drop/drag a
marker) instead of typing raw latitude/longitude. This complements the mobile
participant map (Leaflet + "Cómo llegar" link) delivered on
`feature/mobile-hint-map-webview`.

**Status:** IMPLEMENTED on this branch (code below). Not yet runtime-verified
against a live key — see "What still needs a human" at the bottom.

**Why a handoff:** different surface from the mobile work; needs a Google Maps
JavaScript API key with billing enabled; loading the Maps/Places SDK is a new
pattern in the web app; and it can't be verified in the current sandbox
(external `maps.googleapis.com` is network-blocked here).

---

## Implementation notes (what actually shipped vs the plan)

- **Config:** var is `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY`, allowlisted in
  `vite.config.ts`, surfaced as `googleMapsApiKey` from `config.ts`
  (`getServerConfig` + `getClientConfig`), documented in `.env.example`.
  **Gotcha the plan missed:** the web service in `docker-compose.dev.yml`
  enumerates env vars explicitly (no `env_file`), so the key was also added to
  its `environment:` map (`${NEXT_PUBLIC_GOOGLE_MAPS_API_KEY:-}`) — otherwise a
  key in `.env` never reaches the container.
- **Loader:** `@googlemaps/js-api-loader@^2` — the `Loader` class is deprecated
  in v2; used the **functional API** `setOptions()` + `importLibrary()` instead.
  New hook `src/lib/use-google-maps.ts` loads `maps` + `places` once (shared
  singleton) and reports `disabled | loading | ready | error`.
- **Places choice:** legacy `google.maps.places.Autocomplete` + classic
  `google.maps.Marker`, per the plan's recommendation.
- **Component:** `src/components/map-location-picker.tsx` — search box + map +
  draggable marker + click-to-place; renders `null` when no key so the manual
  lat/lng inputs remain the working fallback. Wired into the `showLocation`
  block of `hint-editor.tsx`, above the manual inputs (both stay in sync).
- **Styles:** `.map-picker` / `.map-picker-canvas` in `index.css` (tokens).
- **Tests:** the web app had **no unit-test runner** — added `vitest` + jsdom +
  `@testing-library/react` (+ `vitest.config.ts`, `vitest.setup.ts`, `npm test`).
  `map-location-picker.test.tsx` mocks the SDK (`setOptions`/`importLibrary`)
  and asserts the no-key fallback (renders nothing, never touches Google) and
  the with-key load path. Green. `tsc`, ESLint (my files), and tests all pass.
- **Pre-existing build blocker found:** a fresh full `tsc -b` fails with 21
  `TS6133` (unused local/param) errors in `live-sessions-workspace.tsx`,
  `missions-admin-workspace.tsx`, `qr-code.ts` — files this branch never
  touched; the same symbols exist on `develop`. So `npm run build` is already
  red on a clean checkout (locally masked by incremental build cache). Not
  fixed here (out of scope); flag before relying on web CI `build`.

---

## Key finding: persistence is already wired

Do **not** add new API/DTO plumbing. Hint coordinates already flow end-to-end:

- Form inputs today: `src/apps/web/src/components/hint-editor.tsx:64-98` — a
  collapsible "Agregar ubicación" block with `Latitud` / `Longitud`
  `type="number"` inputs, written via `onUpdate("latitude"|"longitude", value)`.
- Draft/wire types already carry lat/lng:
  - `HintDraft` (strings) — `mission-authoring-types.ts:178-185`
  - `HintPayload` (request `number | null`) — `:48-55`
  - `HintResponse` (GET) — `:107-114`
- Mapping + validation already exist:
  - draft→payload `serializeHintDraft` — `mission-authoring-model.ts:371-380`
  - response→draft `toHintDraft` — `:246-255`
  - both-or-neither + numeric validation — `:523-535`
- Save path: mission tree is `POST`/`PUT` as one payload —
  `missions-admin-workspace.tsx:310-359` (base URL
  `${edgeProxyPublicBaseUrl}/mission-management/api/mission-management/missions`).

**Implication:** the picker only has to set the two existing draft string fields
via the existing `onUpdate` callback. No backend, DTO, or endpoint changes.

---

## Work to do

### 1. API key config (2 edits — the allowlist step is easy to miss)

The web reads config via `src/apps/web/src/lib/config.ts` which uses
`process.env.*`, statically replaced at build time by
`vite.config.ts:39-41` (`define: { 'process.env': umbralEnv }`).

Only an **allowlisted** set of vars reaches the browser
(`vite.config.ts:12-22`). So:

1. Add the key name to the `umbralEnv` allowlist in `vite.config.ts:12-22`.
   Follow the existing naming convention (`NEXT_PUBLIC_*`), e.g.
   `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY`. (A raw `VITE_` var read via
   `import.meta.env` would be a new, inconsistent pattern — avoid.)
2. Surface it from `config.ts` `getClientConfig` (`:11-40`) as e.g.
   `googleMapsApiKey`.
3. Document it in the repo-root `.env.example` near the web vars
   (`.env.example:47-54`). Leave the value empty (it's committed).

Note: a Maps **JS** key is embedded in the browser (not secret); protect it by
HTTP-referrer restriction in Google Cloud + enabling only **Maps JavaScript
API** and **Places API**. Billing must be enabled on the GCP project.

### 2. Maps JS SDK loader (new pattern — none exists today)

There is no external `<script>` loader in `src/apps/web` today. Add a small hook
`useGoogleMaps()` that injects the loader script once and resolves when ready.

- Prefer the official loader `@googlemaps/js-api-loader` (add to
  `src/apps/web/package.json`) over hand-rolling a `<script>` tag — it dedupes
  and handles the `libraries: ["places", "marker"]` param.
- Guard for a missing key: if `googleMapsApiKey` is empty, render the existing
  numeric inputs only (graceful fallback, and keeps unit tests / offline dev
  working).

### 3. Places API choice — decide up front

Google is migrating Places. Pick one and note it in the PR:
- **New**: `PlaceAutocompleteElement` (web component) + `google.maps.marker.AdvancedMarkerElement`. Recommended for new code; needs the `marker` library and a Map ID.
- **Legacy**: `google.maps.places.Autocomplete` + classic `Marker`. Simpler, widely documented, but on a deprecation path.

Recommendation: **legacy `Autocomplete` + classic `Marker`** for the smallest,
best-documented first cut; revisit if Google sunsets it.

### 4. `MapLocationPicker` component

New file `src/apps/web/src/components/map-location-picker.tsx`. Props:
`{ latitude: string; longitude: string; onChange: (lat: string, lng: string) => void }`.

Behavior:
- Renders a Places search `<input>` + a map (`~220px` tall).
- Selecting a place, clicking the map, or dragging the marker → call
  `onChange(lat.toString(), lng.toString())`.
- Initialize the marker/center from incoming lat/lng if present, else a sensible
  default center.
- Keep the two numeric inputs visible below as a manual fallback / display.

### 5. Wire into `hint-editor.tsx`

Inside the existing `showLocation` block (`hint-editor.tsx:64-98`), render
`<MapLocationPicker latitude={hint.latitude} longitude={hint.longitude}
onChange={(lat, lng) => { onUpdate("latitude", lat); onUpdate("longitude", lng); }} />`
above (or replacing) the raw inputs.

### 6. Styling

Reuse design tokens from `src/apps/web/src/index.css`: `.form-group`
(`:817`), `.form-label` (`:823`), `.form-input` (`:829`), `.form-hint`
(`:867`), `.form-row` (`:877`), card container `.node-item` (`:1425`). Add a
`.map-picker` block near the form styles for the map canvas height/border.

---

## Testing & caveats

- **Cannot be verified in this sandbox** (external Maps host blocked). Verify on
  a real machine with a valid key: `docker compose -f docker-compose.dev.yml up`,
  open the web console `:3000`, Admin → edit a treasure-hunt mission → a hint →
  "Agregar ubicación" → search a place → confirm the numeric fields populate and
  the mission saves.
- **Unit tests**: jsdom can't load the Maps SDK. The `useGoogleMaps` hook must
  no-op without a key so existing tests render the numeric-input fallback. Add a
  focused test that the picker falls back when `googleMapsApiKey` is empty.
- **CSP**: no Content-Security-Policy exists in `src/apps/web`
  (`index.html`, `vite.config.ts`). The `edge-proxy` service **was audited this
  time** (`grep` for CSP / security headers in `src/apps/edge-proxy`): it injects
  **no** CSP either, so nothing blocks the SDK today. If CSP is added later,
  allow `*.googleapis.com` / `*.gstatic.com` for `script-src` / `img-src` /
  `connect-src`. Noted as future hardening, not done here.

## Prerequisites (owner action, not code)

1. GCP project with **billing enabled**.
2. Enable **Maps JavaScript API** + **Places API**.
3. Create a browser key, restrict by HTTP referrer, put it in the web `.env`
   as `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY` (empty → picker hidden, manual inputs work).

## Commit slices (all implemented on this branch)

1. ✅ config wiring (vite allowlist + `config.ts` + `.env.example`).
2. ✅ `useGoogleMaps` loader hook + `@googlemaps/js-api-loader` dep.
3. ✅ `MapLocationPicker` component + `.map-picker` styles.
4. ✅ integrate into `hint-editor.tsx`.
5. ✅ fallback unit test (added vitest infra to enable it).

## What still needs a human

1. **GCP setup** — the prerequisites above (billing, APIs, restricted key).
2. **Runtime verification** with a live key (blocked in this sandbox): the
   manual smoke test under "Testing & caveats".
3. **Decide on the 21 pre-existing `tsc -b` errors** (see Implementation notes)
   so `npm run build` can go green — independent of this feature.
4. **Commit / merge** — the implementation is currently uncommitted on
   `feature/web-hint-location-map-picker` (the plan doc is its only commit).
