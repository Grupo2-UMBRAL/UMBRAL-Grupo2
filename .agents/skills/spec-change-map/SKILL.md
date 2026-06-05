---
name: spec-change-map
description: Routes project changes to the correct spec files and documents the propagation path across ERS, ADRs, CONTEXT docs, agents, and skills. Use when requirements, technology constraints, terminology, architecture decisions, or agent workflows change and you need to know what to edit.
---

# Spec Change Map

## Quick start

Read [../../../README/mapeo de Specs.md](../../../README/mapeo%20de%20Specs.md) first.

Then classify the change into one of these buckets:

- requirement or use case
- architecture decision
- domain terminology
- technology constraint
- agent or skill workflow

## Workflow

1. Identify the source of truth that changed.
2. Update that source first.
3. Follow the propagation path in `README/mapeo de Specs.md`.
4. Check whether the change also affects:
   - `docs/product/ers.md`
   - `src/*/CONTEXT.md`
   - `docs/architecture/adr/`
   - `.agents/agents/`
   - `.agents/skills/`
5. Report what was changed and what intentionally stayed untouched.

## Decision rules

- If the system behavior changed, update the ERS first.
- If the meaning of a domain term changed, update `CONTEXT.md` first.
- If the choice is hard to reverse and needs justification, create or update an ADR.
- If only the assistant workflow changed, update agents or skills, not the domain docs.
- If a professor-imposed technology changed, reflect it in the ERS and then decide whether it deserves an ADR.

## Output format

When using this skill, respond with:

1. change category
2. source of truth to edit first
3. propagation list in order
4. files that do not need changes

## Reference

See [../../../README/mapeo de Specs.md](../../../README/mapeo%20de%20Specs.md).
