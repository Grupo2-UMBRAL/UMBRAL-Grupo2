# UMBRAL Visual Direction

This document captures the working UI direction agreed for UMBRAL so it can be reused during design and implementation.

## 1. Product surfaces

UMBRAL has two distinct experiences:

- Mobile for participants.
- Web for administrators and operators.

The mobile experience should feel more playful and guided. The web experience should feel calmer, more operational, and more information-dense.

## 2. Mobile participant flow

The participant flow should feel inspired by Duolingo in the way progress is shown, not in a literal visual copy.

### Main screens

- Login.
- Join session by code.
- Team dashboard.
- Send evidence.
- Ranking.
- Hints and revealed solutions.

### Mobile layout rules

- One primary action per screen.
- Linear progression, not dense menus.
- Large touch targets.
- Short labels and short feedback messages.
- Visible progress path or stage map.

### Mobile interactions

- QR scan for Treasure Hunt sessions.
- Text answer input for Trivia sessions.
- Immediate feedback after submission.
- Clear connection state and sync state.

## 3. Web operational flow

The web interface should prioritize clarity, speed, and situational awareness.

### Main screens

- Login.
- Operational home.
- Session dashboard.
- Team detail drawer.
- Mission management.
- Session management.
- User management.

### Web layout rules

- Strong hierarchy at the top of the page.
- Main content in the center.
- Context details in a side panel or drawer.
- Tables and rankings should be easy to scan.
- Actions should be visible but not noisy.

### Web interactions

- Use live ranking and event history as core operational views.
- Use drawers for contextual detail.
- Use modals only for irreversible or high-risk actions.
- Prefer inline edits or side panels for routine changes.

## 4. Visual language

The web operator app stays warm, soft, and calm; the mobile participant app uses a brighter,
Duolingo-inspired palette (vivid green/blue/red/yellow on a near-white background). Both keep
soft rounded surfaces and clear hierarchy. For the mobile shapes, color tokens, and component
rules, see [`mobile-shape-language.md`](./mobile-shape-language.md).

### What to keep

- Soft rounded surfaces.
- Light, soft backgrounds (mobile: bright near-white; web: warm neutral).
- Clear typographic hierarchy.
- Pills, badges, and compact filters.
- A single focal area per screen.

### What to avoid

- Generic SaaS dashboards.
- Overloaded grids of identical cards.
- Modal-first interaction design.
- Decorative motion that does not explain state.
- Excessive gray-on-gray interfaces.

## 5. Color and state guidance

Color should guide meaning, not decorate.

- Green for progress, success, and completion.
- Blue for neutral navigation and information.
- Amber for attention, pending actions, and new hints.
- Red for errors, blocking states, and destructive actions.

Backgrounds stay soft, never harsh: the mobile participant app uses a bright near-white "Polar"
tone (`#F7F7F7`, see the mobile shape-language doc); the web operator app keeps a warm neutral.
Neither uses pure white for the page background.

## 6. Interaction hierarchy

Use this rule of thumb:

- If the user needs context to decide, use a drawer or side panel.
- If the user must confirm a risky action, use a modal.
- If the user only needs to be informed, use a toast or banner.

## 7. Priority build order

If implementation starts from the UI, build in this order:

1. Login.
2. Join session.
3. Mobile dashboard.
4. Session dashboard on web.
5. Team detail drawer.
6. Mission management.

## 8. Working summary

- Mobile: friendly, playful, guided.
- Web: clear, calm, operational.
- Both: simple, warm, and easy to scan.
