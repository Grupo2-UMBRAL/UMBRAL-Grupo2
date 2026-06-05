# Examples

## Example 1

Change: `SignalR` replaces generic WebSockets as a required implementation.

- Category: technology constraint
- Edit first: `ERS UMBRAL .md`
- Propagate to:
  - `docs/product/ers.md`
  - `docs/architecture/adr/` if the team wants the trade-off recorded
  - `.agents/agents/backend-agent.md`
  - `.agents/agents/devops-agent.md`

## Example 2

Change: `Session Team` is renamed.

- Category: domain terminology
- Edit first: `src/session-operations/CONTEXT.md`
- Propagate to:
  - `docs/product/glossary.md`
  - `docs/product/ers.md`
  - affected agents
  - affected skills

## Example 3

Change: a new use case is added.

- Category: requirement or use case
- Edit first: `ERS UMBRAL .md`
- Propagate to:
  - `docs/product/ers.md`
  - `open-questions.md` if it introduces ambiguities
  - agents only if their workflow assumptions changed
