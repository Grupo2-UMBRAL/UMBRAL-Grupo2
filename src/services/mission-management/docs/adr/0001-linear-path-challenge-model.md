---
status: accepted
---

# Linear playable path over an inert recursive Section composite

## Context

The Mission aggregate modelled its content as a recursive tree of `MissionNode` serialized into a single `NodeTreeJson` column. Each node was either composite (structure only) or leaf (a playable `Mission Stage`), with cross-field rules enforcing the duality and a `DefaultTimeBudget` that inherited down the subtree. Trivia was free-form text matched against a single `TriviaValidAnswer`.

Problems: (1) the node tree mirrored the editor component tree, leaking UI vocabulary into the domain; (2) every playable unit was a full leaf node that repeated Game Type, Difficulty, Prompt, validation and time, so five trivia questions meant five near-identical leaves; (3) trivia had no fixed choices or marked correct option.

A course requirement also mandates demonstrable **recursive substages**.

## Decision

- A **Mission** owns an ordered list of **Path Items**. A Path Item is either a **Challenge** (the playable leaf) or a **Section** (an organizational composite). This is the Composite pattern: `PathItem` = Component, `Section` = Composite, `Challenge` = Leaf.
- **Recursion is confined to Sections.** A Section may contain other Sections, satisfying the recursive-substage requirement. A Section is **inert**: `Title` + `Order` + children only, with no Game Type, Difficulty, time, validation, or inheritance. Game semantics never live on a composite.
- **The playable path is the depth-first, in-order flatten of the leaf Challenges**, so the experience stays linear. Recursion is optional: a Challenge may sit directly at the Mission root, so a simple mission is a flat list and only complex ones nest Sections.
- A **Challenge** is a typed, playable block that holds an ordered list of **homogeneous plays** and carries the shared configuration once (Game Type, default Difficulty, default Time Limit). Challenges never nest.
  - **Trivia Challenge** holds **Questions**: text + 2..4 **Choices** with exactly one correct (Kahoot-style), replacing the free-form answer.
  - **Treasure Hunt Challenge** holds ordered **Searches**: clue + expected QR hash + its own **Hints**.
- **Two fixed levels of game config, explicit override, no inheritance chain.** A play may override Difficulty / Time Limit; the effective value is `play.X ?? challenge.X`. This replaces the recursive `DefaultTimeBudget`. The Section tree carries nothing down.
- **The scored atom is the play** (Question / Search), not the Challenge. Mission Management stores only Difficulty and the correct answer; point calculation (including the Kahoot speed bonus) stays in `scoring-monitoring`, which already consumes `Difficulty` + `ResolutionTime`. The cross-context credit contract keeps its shape; "stage" now means "play".
- **Mission no longer authors `GameType` or `Difficulty`** — both are derived from its Challenges. An optional overall maximum duration remains.
- **Persistence is relational.** The `NodeTreeJson` blob is dropped. The pre-release database is disposable, so no data migration is required.

## Considered Options

- **Keep the old `MissionNode` tree, flatten only in the UI** — rejected: leaves the domain coupled to the structure that caused the pain.
- **Flat, non-recursive dividers (a `SectionBreak` with no nesting)** — rejected: simplest, but fails the course's recursive-substage requirement. Replaced by inert recursive Sections, which add recursion without game semantics.
- **Recursive *playable* Challenges (nest blocks/plays)** — rejected: satisfies the requirement literally but revives the composite/leaf duality and per-leaf repetition.
- **Inheriting Sections (push Difficulty/time down the tree)** — rejected: revives the recursive `DefaultTimeBudget` complexity. Sections stay inert.
- **Branching graph (answers route to different next steps)** — rejected: contradicts the clear-path goal.

## Consequences

- Retire `MissionNode` (composite/leaf), the legacy `MissionStage`/`MissionStageHint`, `NodeTreeJson`, and the authored `Mission.GameType` / `Mission.Difficulty`.
- The recursion the course requires is demonstrable as a textbook Composite, but isolated to inert organization; the playable model and the 2-level Challenge to play config are unaffected by depth.
- `session-management` and `scoring-monitoring` keep calling the scored unit a "stage"; per the context map that translates to a Mission Management **play**.
- New invariants: a Section is inert (no game fields); a Challenge never nests; a Question has 2..4 Choices with exactly one correct; the flatten of a Mission must yield at least one active Challenge with at least one play for it to be eligible for a LiveSession.
- The glossary in `../../CONTEXT.md` reflects this language; the playable "tree"/"node"/"stage" terms remain retired, while `Section` is the only recursive concept.
