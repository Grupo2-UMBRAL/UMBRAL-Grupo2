# Web dashboard refactor — follow-ups for a later session

Discovered during the operator/admin dashboard refactor based on
`design_handoff_missions_redesign/`. Ground rule for the whole refactor:
**do not change the endpoints the web already calls.**

## Done in this pass
- **Palette → mobile Duolingo-bright.** `index.css` `:root` tokens flipped from the dark/amber
  theme to the mobile light palette (near-white `#F7F7F7`, green `#58CC02`, blue `#1CB0F6`,
  yellow `#FFC800`, red `#FF4B4B`, text `#4B4B4B`). `color-scheme: light`, shadows softened,
  4 hardcoded white overlays flipped to dark. Existing components were NOT individually
  re-audited for pixel parity with mobile (per Daniel: layout matters more than parity).
- **Synchronized flow board** (operator live view, `live-sessions-workspace.tsx`):
  - Left column per-team list → **team grid** (rank badge, score, participants, hints, and a
    "Etapa lista · esperando" / "En curso" sync chip). Selection still opens the team detail.
  - Resumen tab stage *status table* → **group stepper + current-stage panel** (done/current/
    upcoming nodes, N/total finished, progress bar, "next stage" hint).
- **Pause / Skip stage** wired in the current-stage panel:
  - Pause/Resume → existing session lifecycle action (`handleLifecycleAction`).
  - Saltar etapa → existing `handleDeactivateStage(currentStage)` (was defined but unwired).
    Disabled on the last stage.
- **Reuse modal (Change 1) confirmed complete** — `reuse-item-modal.tsx` already pulls real
  sections/challenges from *other* missions via `fetchMissionDetail`; deep-clone regenerates
  clientIds and drops server ids. Fixed a real compile break: `SectionDraft`/`ChallengeDraft`
  were used but not imported in `missions-admin-workspace.tsx`.

## Second pass (dark-blue redesign)
- Palette switched again to **dark-blue** (Duolingo dark mode): `index.css` `:root`. "Blanco muy blanco" fixed.
- Operator live view rebuilt: **killed the 3-column layout** → header (title/status/En Vivo–Planificar toggle/SignalR/timer) + left vitals rail (4 stat tiles, Control de sesión, Clasificación, Actividad en vivo) + main flow board. All in `.ops-*` classes.
- **Team detail drawer** added (right overlay): Puntaje/Puesto/Jugadores, Recorrido de etapas, Liberar pista (+ create operational hint), Evidencias/override, Aplicar penalización (severity buttons). Reuses every existing handler.

## Third pass (mission-design redesign)
- **Mission builder rebuilt to the 3-pane "Diseño de misiones" layout**
  (`mission-builder-full.tsx` + new `mission-list-column.tsx` + rewritten
  `mission-builder-canvas.tsx`, styled in `mission-builder.css`):
  - `.mb-root` no longer covers the app rail — it starts at `left: var(--sidebar-w)`
    and follows `.sidebar-collapsed`, so the UMBRAL sidebar stays visible.
  - **Missions list column** (middle pane): scrollable, **collapsible** (chevron
    in its header; a reopen chevron appears in the top bar), mission cards with
    status dot + `Nm` + Activa/Inactiva. Selecting loads that mission without
    leaving the editor; `+` creates a new one.
  - **Main canvas**: editable title/description, **stat tiles**
    (Secciones/Retos/Preguntas/Búsquedas/Duración máx — duration is editable),
    then **Estructura** with `+ Sección` / `+ Reto` (both open the reuse modal).
  - **Structure nodes** are collapsible cards (drag-to-reorder handle, type glyph,
    title, meta subtitle, Sección/Trivia/Tesoro badge, chevron). Expanding a
    challenge reveals the full `ChallengeEditor`; sections expand to their
    children + nested add buttons. DnD reorder kept per sibling scope.
  - Old tree sidebar (`mission-builder-sidebar.tsx`) **deleted** (fully replaced).
  - Launch/close preserved: the stacked admin page (ServiceStatusBoard +
    OperatorUsers + catalog) is unchanged; the catalog opens the overlay, and a
    **Volver** button closes it.
- **Missing endpoints** the redesign surfaces are catalogued in
  `docs/endpoints_faltantes.md` (list counts / `N etapas`, a reusable-items
  endpoint, optional mission delete). `MissionListColumn` already accepts a
  `stageCounts` prop for #1 once the contract carries counts.
- `tsc -b --force` clean. (vite/eslint bins missing from this machine's partial
  `node_modules` — couldn't run a full bundle/lint here.)

## To implement / decide (next session)
- [ ] **Planning wizard** — still the OLD list/form (`~3444-3756`). Replace with the "Arma una LiveSession en 3 pasos" 3-step layout (Misión origen / Flujo de etapas Quitar+Agregar / Programar). Keep loadMissions, loadMissionDetail, toggleMissionStageSelection, moveSelectedMissionStage, handleCreateLiveSession + the persisted-sessions list.
- [ ] **"Inactivos" stat tile** shows 0 — per-team inactivity isn't on the overview list (only on team detail). Needs a data source or drop the tile.
- [ ] **"En Vivo / Planificar" toggle** — "Planificar" currently just exits to the planning view (same as the old "Volver al menú"). Fine for now; revisit if a true in-session plan mode is wanted.
- [ ] **Skip-stage semantics** — "Saltar etapa" reuses the stage *deactivation* endpoint
      (`POST /stages/{missionStageId}/deactivate`). Confirm that deactivating the current group
      stage is the correct way to force the whole group forward, or whether a dedicated
      "advance group" backend op is needed.
- [ ] **Pause-stage vs pause-session** — the flow board maps "Pausar etapa" to the existing
      *session* pause. Confirm whether the backend distinguishes a stage-level pause; if so,
      point the button at that action.
- [ ] **Admin operator listing** — LEFT UNTOUCHED per Daniel's instruction.
- [ ] **Nunito font (optional)** — mobile uses Nunito; web still uses IBM Plex Sans. Switching
      needs `@fontsource/nunito` + a token change. Not done (palette-only scope).

## Pre-existing tech debt found (not caused by this refactor)
- Frontend `tsc -b` was already red: ~16 `TS6133` unused-declaration errors across
  `qr-code.ts`, `mission-builder-sidebar.tsx`, `missions-admin-workspace.tsx`, and
  `live-sessions-workspace.tsx`. (The web image likely builds via Vite/esbuild, bypassing
  the `tsc` gate.) My new flow-board code is type-clean.
- **Dead code**: `LiveSessionOverviewDashboard` + its helpers (`TeamStageProgress`,
  `getTeamStageProgress`, `sortOverviewTeams`) and the `LiveSessionOverviewDashboardProps`
  type in `live-sessions-workspace.tsx` are orphaned (never rendered since the bento/3-column
  redesign, commit `21fe367`). Safe to delete (~230 lines) — recommend removing next pass.
