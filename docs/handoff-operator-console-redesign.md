# Handoff — Web operator/admin console redesign

**Goal:** port the design prototype in `design_handoff_missions_redesign/` into the real web app
(`src/apps/web`, React 19 + Vite + TS), preserving all behavior and **without changing any
backend endpoints**. Palette is the Duolingo **dark-blue** theme with a light-mode toggle.

Read `CLAUDE.md` (repo root) first for the project map, then this file.

---

## Status at handoff

### ✅ Done
1. **Palette = dark-blue (Duolingo dark)** + **light-mode toggle.**
   - Tokens: `src/apps/web/src/index.css` `:root` (dark, default) and `:root[data-theme="light"]`
     (light override). Accent = blue `#1cb0f6`; bg `#131f24`.
   - Toggle: `src/apps/web/src/components/dashboard-layout.tsx` — `theme` state + `useEffect`
     sets `data-theme` on `<html>`, persisted to `localStorage["umbral-theme"]`. Button (sun/moon)
     lives in `.content-header-actions`.
2. **Operator LIVE dashboard rebuilt** (`src/apps/web/src/components/live-sessions-workspace.tsx`,
   the branch guarded by `if (selectedLiveSessionId && selectedLiveSession)`), replacing the old
   3-column layout with the prototype layout, all `.ops-*` classes (see `index.css` §30):
   - Header: title + estado badge + **En Vivo / Planificar** toggle + SignalR + timer.
   - Left **vitals rail** (`.ops-sidebar`): 4 stat tiles (En juego / En meta / Inactivos / Equipos),
     Control de sesión (lifecycle buttons), Clasificación (ranking), Actividad en vivo (event log).
   - Main (`.ops-main`): Tablero de flujo header → group stepper → current-stage panel
     (Pausar/Reanudar + Saltar etapa) → team grid. Scheduled sessions show the enrollment +
     create-team panel instead.
3. **Team detail drawer** (right overlay, opens on team/ranking click): Puntaje/Puesto/Jugadores,
   Recorrido de etapas, Liberar pista (+ create operational hint), Evidencias + override,
   Aplicar penalización (Menor/Mayor/Crítica + motivo). Reuses every existing handler.
4. `ServiceStatusBoard` removed from the operator view (`OperatorPage.tsx`) — it was showing above
   the live console. Still present on the Admin page.
5. Earlier passes: synchronized flow board, pause/skip wiring, reuse-modal `SectionDraft`/
   `ChallengeDraft` import fix. See `docs/web-refactor-followups.md`.

### ⏳ Remaining (primary next task)
**Planning wizard** — the "no session selected" branch (the second `return` in
`LiveSessionsWorkspace`, root `<div className="workspace-section">`, currently ~lines 3450-3760)
is STILL the old list/form. Replace it with the prototype's **"Arma una LiveSession en 3 pasos"**
(screenshot `05-…` / prototype `isPlan` view):
- Header eyebrow "PLANIFICAR SESIÓN" + h2 "Arma una LiveSession en 3 pasos".
- 3 step cards: **1 Elegí misión** (Del catálogo activo) · **2 Recortá el flujo** (Etapas y orden)
  · **3 Programá** (Nombre e inicio).
- Two panels: left **Misión origen** (list of `missions`, card = name + "N etapas" badge +
  "Xmin · types"); right **Flujo de etapas** (subtitle "Recorta y reordena sin tocar el diseño de
  la misión", "N en el flujo" counter, each row: #order, type icon, name, "type · diff · min",
  **Quitar/Agregar** button + reorder).
- Footer: session-name input + **Programar** button.
- **Also keep** the persisted live-session list somewhere so the operator can enter a session
  (sets `selectedLiveSessionId` → live mode).

**Handlers/state to reuse verbatim (DO NOT change endpoints):**
`missions` (EligibleMissionSummary[]), `selectedMissionId`, `selectedMission`,
`selectedMissionStageIds` (array order = session-stage order), `draft` (name + scheduledStart),
`loadMissions`, `loadMissionDetail`, `toggleMissionStageSelection(missionStageId)`,
`moveSelectedMissionStage(missionStageId, ±1)`, `draftPreview` memo, `handleCreateLiveSession(event)`
(POST `${liveSessionsUrl}` body `{ missionId, name, scheduledStartAtUtc, selectedMissionStageIds }`),
and the persisted list mapping `liveSessions` → click sets `selectedLiveSessionId`.
Add `.ops-*`-style classes for the wizard in `index.css` (theme tokens only).

### ⏳ Polish / smaller items
- **"Inactivos" stat tile** shows `0` — per-team inactivity is only on the team-detail response,
  not the overview list. Needs a data source or drop the tile. (`inactiveCount` const is hardcoded 0.)
- **Light-mode edge cases**: a few `rgba(255,255,255,x)` overlays in `index.css` (grid texture +
  2 subtle backgrounds) go invisible in light mode. Convert to theme-aware if it bothers.
- **"En Vivo / Planificar" toggle**: "Planificar" currently just calls `setSelectedLiveSessionId(null)`
  (exits to the planning view). Fine unless a true in-session plan mode is wanted.
- **Dead code**: `LiveSessionOverviewDashboard` + `TeamStageProgress` + `getTeamStageProgress` +
  `sortOverviewTeams` + `LiveSessionOverviewDashboardProps` in `live-sessions-workspace.tsx` are
  orphaned (never rendered since the bento redesign). Safe to delete ~230 lines.

---

## Gotchas
- **The prototype is partly stale.** It references `LiveSessionOverviewDashboard`; that component
  is dead. The real live view was inline JSX (now the `.ops-*` tree). Change 1 (reuse modal) was
  already implemented before this work.
- **Frontend `tsc -b` is pre-existingly RED** (~16 `TS6133` unused-var errors across
  `qr-code.ts`, `mission-builder-sidebar.tsx`, `missions-admin-workspace.tsx`,
  `live-sessions-workspace.tsx`). The web image builds via Vite/esbuild, which does not gate on
  these. All redesign code added in this effort is type-clean; verify new work the same way.
- **Endpoints are frozen.** Reuse existing handlers; never invent routes. "Saltar etapa" reuses the
  stage-deactivate endpoint; confirm that semantic with backend (see followups).
- Vocabulary: **etapa / misión / sesión** (never "sección"/"play"); wire "play"→"missionStage"
  adapters already exist (`toEligibleMission*`).

## Build / verify / run
- **Typecheck (from `src/apps/web`):** `node node_modules/typescript/lib/tsc.js -b`
  (the `.bin/tsc` shim is broken in this install; call `lib/tsc.js` directly). Filter out
  `TS6133`/`TS6192` to see real errors.
- **Run the whole stack:** `docker compose -f docker-compose.dev.yml up -d` (or
  `scripts/Start-Dev.ps1`). Web `:3000`, edge-proxy `:7500`. DB is disposable (pre-release).
- **Web dev only:** `npm run dev` in `src/apps/web` (shows layout/theme with empty data if the
  backend isn't up).

## References
- Prototype: `design_handoff_missions_redesign/prototype/UMBRAL Console.dc.html` (+ `support.js`);
  screenshots in `design_handoff_missions_redesign/screenshots/` and the READMEs there.
- Running follow-ups: `docs/web-refactor-followups.md`.
- Endpoints the web calls: `docs/architecture/frontend-endpoints-summary.md`.
- Roles: `docs/architecture/authorization-matrix.md`.
