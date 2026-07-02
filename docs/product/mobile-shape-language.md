# UMBRAL Mobile — Shape & Illustration System

Concrete, implementable companion to [`design.md`](./design.md). That document sets the
direction ("Duolingo-inspired progress, not a literal copy"); this one turns it into shapes,
tokens, and component rules the mobile client (`src/apps/mobile`) can build against.

Source: Duolingo's public brand guidelines, *Illustration → Shape Language*
(design.duolingo.com/illustration/shape-language). Principles are paraphrased and adapted to
UMBRAL's existing warm palette and game vocabulary (sesión / misión / etapa) — see
[`front-vocabulary-etapa-mision-sesion`] convention already in place.

## 1. Construction — three shapes only

Every surface, icon, and future illustration is built from three primitives. No pointy corners,
ever.

| Shape | UMBRAL usage today | Where to extend |
| --- | --- | --- |
| **Rounded rectangle** | Cards (`HintCard`, `ProgressTrack` node card, `ScreenShell` hero), buttons, chips | Default container for any new surface |
| **Circle** | Stage dots in `ProgressTrack`, connection-status dot | Team avatars, rank badges, notification dots |
| **Rounded triangle** | Not used yet | "You are here" pointer on the stage path, direction hints on `StaticHintMap`, a caret for expandable cards |

Rule of thumb: if you're about to draw a shape with a sharp corner (a flag, a star point, an
arrow), round it or rebuild it from a rectangle + circle instead.

## 2. Rhythm & simplicity

- Vary shape size and weight inside one screen — a hero card, a row of small chips, and a single
  big CTA read better together than three identical cards stacked.
- Each screen keeps **one primary action** (already the rule in `design.md` §2) — rhythm reinforces
  this: the primary button should be visually the "long note," everything else shorter.
- Cap decoration per illustration/icon at roughly 10–20 shapes. Fewer reads as empty; more hurts
  legibility at small sizes (a stage-map icon is often rendered under 40dp).
- Current gap: `ProgressTrack`'s state glyphs (`✓`, `★`, `🔒`) are raw emoji — they render
  inconsistently across platforms/fonts and break the shape system. Replace with small custom
  glyphs built from the same rounded-rectangle/circle/triangle vocabulary (a filled circle for
  done, a rounded triangle "pointer" for current, a rounded-rectangle padlock for locked).

## 3. Objects in space (flat depth)

- Everything is drawn on a **flat perspective** — depth comes from layering and color, not
  gradients or 3D shading. This already matches the codebase: no `boxShadow`/elevation is used
  today.
- If a shadow is ever needed (e.g. a floating action button, a celebratory modal), it must be a
  **pill**, never an oval or a soft blurred blob — ovals imply perspective, which breaks the flat
  system.
- Shadow color is always a darker, flat tint of what it sits on — never gray, never a generic
  black at low opacity.
- Don't re-color an object just to imply it sits "behind" another — prefer overlap and size
  instead of extra hues (keeps the palette below).

## 4. Color system

Duolingo's rule: pastels over white, never gray, keep each illustration to a handful of colors,
let color carry meaning. UMBRAL's palette already follows this (warm base, no pure white/gray
backgrounds) — this table makes the semantics explicit so new screens reuse the same values
instead of inventing new hex codes.

### Base surfaces

| Token | Hex | Usage |
| --- | --- | --- |
| `surface.base` | `#f6efe6` | Screen background (`SafeAreaView`) |
| `surface.card` | `#fffaf5` | Default card / hero background |
| `surface.cardBorder` | `#eadcc8` | Default card border |
| `surface.raised` | `#f2f6f7` | Secondary panel background |

### Text

| Token | Hex | Usage |
| --- | --- | --- |
| `text.primary` | `#17313b` | Titles, card content, labels on light surfaces |
| `text.secondary` | `#4d5e64` | Descriptions, captions |
| `text.muted` | `#6b7a72` / `#8a978f` | Sublabels, locked-state text |
| `text.onBrand` | `#f7fbfc` | Text/icons on filled brand backgrounds |

### Brand

| Token | Hex | Usage |
| --- | --- | --- |
| `brand.primary` | `#2d6a4f` | Primary CTA, completed stage, progress fill |
| `brand.primaryTint` | `#d8f3dc` / `#eef5f1` | Success backgrounds, completed-state fills |
| `brand.primaryStrong` | `#1b4332` | Pressed/active state |
| `brand.secondary` | `#1e6f8c` | Secondary CTA, current stage, nav accents |
| `brand.secondaryTint` | `#d8ecf5` / `#eaf6fa` | Info backgrounds, current-stage card fill |

### State (meaning-first, per `design.md` §5)

| State | Fill | Text/Icon | Meaning |
| --- | --- | --- | --- |
| Success / progress | `#dff2dd` / `#d8f3dc` | `#25613a` / `#2d6a4f` | Completed etapa, accepted evidence |
| Info / neutral nav | `#d8ecf5` / `#eef7fb` | `#175f78` / `#1e6f8c` | Current etapa, connection info |
| Warn / hint | `#f9e8c7` / `#fff1df` | `#8a5d14` / `#d28b39` | Released pista, revealed solución |
| Error | `#f7d9d9` / `#ffe5e5` | `#9e2f2f` / `#d94f30` | Rejected evidence, connection error |
| Locked / disabled | `#e4ebe1` | `#8a978f` | Locked etapa, disabled action |

Guardrail: don't add a new hex value for a one-off screen. If none of the above fit, extend this
table first so the palette stays closed and consistent — that's the whole point of the "few
colors, used consistently" rule.

## 5. Floating accents

Use sparingly, and only when they carry information:

- A small badge floating above a `ProgressTrack` node when a new hint unlocks.
- A rank-change indicator floating beside a row in `Ranking` when a team moves up/down.
- Never float something purely decorative — Duolingo's own rule is "don't force it."

## 6. Shape tokens (radius, stroke, spacing, type)

Formalizes values already scattered across components so new ones stay consistent.

| Token | Value | Usage |
| --- | --- | --- |
| `radius.sm` | 12 | Small chips, inline tags |
| `radius.md` | 18 | Cards (`HintCard`, `ProgressTrack` card) |
| `radius.lg` | 24 | Hero/section containers (`ScreenShell` card) |
| `radius.pill` | 999 | `StatusChip`, progress bar fill, dot-shaped shadows |
| `radius.control` | 20 | Buttons (soft-rounded, not a full pill) |
| `stroke.default` | 1px | Default card border |
| `stroke.emphasis` | 2px | Current/selected state (current stage card, solution card) |
| `stroke.heavy` | 3px | Large circular nodes (`ProgressTrack` dot ring) only |
| `space.xs` – `space.xl` | 4 / 8 / 12 / 16 / 20 / 24 | Reference scale for new gaps/padding — existing components keep their current numbers unless the layout is being touched anyway |
| `type.title` | 32/38, weight 800 | Screen hero title |
| `type.cardTitle` | 18/24, weight 700 | Card headings |
| `type.body` | 16/24, weight 400–600 | Body copy, hint content |
| `type.caption` | 13/19, weight 700 | Sublabels, progress captions |
| `type.eyebrow` | 12, weight 700, uppercase, +1.2 tracking | Section eyebrows |

## 7. Component checklist (what to fix when touching each file)

- All 8 shared components (`game-button`, `status-chip`, `hint-card`, `progress-bar`,
  `progress-track`, `screen-shell`, `static-hint-map`, `loading-screen`) now import color values
  from `colors` in `tokens.ts` instead of inlining hex. Radius/spacing/type numbers were left as
  literals — see the note on `space` above.
- `progress-track.tsx` — still uses emoji glyphs (§2); replacing them with shape-based icons is
  unstarted.
- Screens (§13) still inline their own hex values — migrate those next, one file at a time.
- Any new component — pull colors from `tokens.ts` instead of inlining new hex values.

## 8. Do / Don't

**Do**
- Build every shape from rounded rectangle, circle, or rounded triangle.
- Keep shadows flat, pill-shaped, and darker-than-surface.
- Vary shape size for visual rhythm; keep each screen simple (fewer, meaningful shapes).
- Assign color by meaning (state tables above), not by screen.

**Don't**
- Don't use pointy corners, ovals, gradients, or 3D shading — breaks the flat-perspective system.
- Don't introduce gray as a background — use the warm pastel tints in §4.
- Don't add a floating accent, a shadow, or a new hex value "because it looks nice" — every
  addition should serve legibility or meaning.

## 9. Tokens file

`src/apps/mobile/src/theme/tokens.ts` implements §4 and §6 (`colors`, `radius`, `stroke`, `space`,
`typography`, `fontFamily`). It is the single source of truth — new hex values or spacing numbers
belong there first, not inlined in a component.

## 10. Typography system

Duolingo pairs a bespoke display face (Feather Bold) with a rounded body face (DIN Next Rounded),
and explicitly names **Nunito** as the open substitute when the custom fonts aren't available
(design.duolingo.com/identity/typography). UMBRAL has no custom-typeface budget, so adopt Nunito
everywhere instead of running two separate families — same rounded, friendly personality, free via
`@expo-google-fonts/nunito`.

Weight carries the two-tier idea instead of a second family:

| Role | Weight | Token |
| --- | --- | --- |
| Hero title / short headline | Nunito ExtraBold (800) | `type.title` |
| Card headings, captions, eyebrows | Nunito Bold (700) | `type.cardTitle` / `type.caption` / `type.eyebrow` |
| Body copy | Nunito SemiBold (600) | `type.body` |

Rules carried over from Duolingo's typography page: never justify text, never hyphenate, avoid
ALL CAPS in running copy (eyebrows/labels are the deliberate, tracked-out exception).

`fontFamily.display` / `fontFamily.body` in `tokens.ts` are `undefined` today (RN system font
fallback). Next step to activate: install `@expo-google-fonts/nunito` + `expo-font`, call
`useFonts` in the root layout, and gate first paint on the existing `LoadingScreen` until the
weights resolve — then set the two tokens to `"Nunito_800ExtraBold"` / `"Nunito_600SemiBold"`.

## 11. Voice & tone

Adapted from Duolingo's four voice qualities (Expressive, Playful, Embracing, Worldly) to UMBRAL's
game copy:

- **Directa** — short verbs, active voice, no filler ("Escanea el QR", "Envía tu respuesta").
- **Alentadora** — celebrate progress, never blame on a wrong answer ("Respuesta incorrecta,
  intenta de nuevo", not "Estás mal").
- **Consistente** — tuteo (tú) everywhere; never voseo or usted. This already matches existing
  copy ("tu equipo", "puedes") — keep it that way.
- **Precisa con el vocabulario del juego** — etapa / misión / sesión / pista / evidencia, never
  "sección", "nivel", or "play" as a synonym (see [[front-vocabulary-etapa-mision-sesion]]).

Tone shifts with stakes, same idea as Duolingo's tone guidance: a stage-completion toast can be
exclamatory ("¡Etapa superada! 🎉"); a connection-error banner should be calm and factual
("Conexión perdida. Reintentando…") — never playful when something is actually broken.

## 12. Character & icon construction

No mascot or custom icon set exists yet, but if/when one is built (starting with replacing the
`ProgressTrack` emoji glyphs), follow Duolingo's character-construction rules:

- Eyes are geometric only — round, pill, or almond. Never ovals.
- Noses/badges are 1–2 rounded rectangles, as saturated as they are dark.
- Hands/limbs abstract to circles; don't enumerate fingers or joints.
- Build from the fewest shapes possible (§1–2); avoid a static, expressionless pose.

Concrete target: replace `✓` / `★` / `🔒` in `progress-track.tsx` with three small custom glyphs
(filled circle for done, rounded-triangle pointer for current, rounded-rectangle padlock for
locked) built from the same three-shape vocabulary, instead of relying on emoji font rendering.

## 13. Token reconciliation (screens not yet migrated)

The shared components (§7) now consume `tokens.ts`. Screens still have ad hoc one-off hex values
from before this system existed. Table for the next pass, so nobody re-derives new colors instead
of reusing these:

| File | Ad hoc hex | Canonical token |
| --- | --- | --- |
| `board.tsx` | `#9e6f00` | `colors.state.warn.text` |
| `join.tsx` | `#98640f` | `colors.state.warn.text` |
| `board.tsx` | `#ef9a9a` | `colors.state.error.border` |
| `board.tsx` | `#9aa6a1` (disabled button fill) | `colors.state.disabled.fill` |
| `board.tsx` | `#b8c8cc` (choice button border) | `colors.surface.raisedBorder` |
| `board.tsx` | `#eef5f1` / `#bed8c9` (resolutions section) | `colors.brand.primaryTintAlt` / `colors.brand.primaryRing` |
| `board.tsx` | `#f2e7de` / `#d1ab89` (finalized notice) | `colors.state.warn.fillMuted` / `colors.state.warn.borderMuted` |
| `board.tsx` | `#000` / `#dce8ea` (scanner modal) | `colors.overlay.scrim` / `colors.overlay.text` |
| `forbidden.tsx` | `#fff8f8` | `colors.text.onBrand` |

## 14. Suggested next step

Migrate the screens in §13, one file at a time, replacing each ad hoc hex with its canonical
token and running `npm run typecheck` + `npm test` after each file.
