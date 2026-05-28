---
name: linear-ticket-orchestrator
description: Orchestrate implementation work from Linear tickets with one branch and one git worktree per ticket, explicit authority boundaries between orchestrator and workers, and disciplined updates back to Linear. Use when Codex needs to pick up ready-for-agent tickets, manage AFK execution, delegate implementation or review, control agent permissions, or keep ticket-to-branch ownership strict.
---

# Linear Ticket Orchestrator

## Read first

1. `AGENTS.md`
2. `.agents/agents/issue-tracker.md`
3. `.agents/agents/orchestrator.md`
4. `.agents/agents/operating-model.md`
5. Relevant `docs/architecture/adr/` if code or workflow boundaries are affected
6. Relevant `src/services/*/CONTEXT.md` if the ticket touches a bounded context

## Goal

Run ticket execution from Linear without letting workers own global repo state.

## Default model

- One orchestrator owns Linear state and `git` state.
- One ticket maps to one branch.
- One ticket maps to one `git worktree`.
- Workers only get the minimum scope needed for their slice.
- Workers do not create commits; the orchestrator consolidates history only after human review.

## Workflow

1. Read the Linear ticket, comments, labels, and acceptance criteria.
2. If context is incomplete, stop and return the ticket to `needs-info` or `needs-triage`.
3. If the ticket is executable, claim it from the orchestrator role.
4. Derive branch name:
   - `feature/<issue-id>-<slug>`
   - `fix/<issue-id>-<slug>`
   - `chore/<issue-id>-<slug>`
5. Create a dedicated `git worktree` for that branch.
6. Dispatch the worker with:
   - ticket ID
   - acceptance criteria
   - bounded context
   - relevant ADRs
   - explicit do-not-cross boundaries
7. Receive worker output and run validation.
8. Decide next state:
   - keep moving toward PR
   - send back to human
   - split into follow-up tickets
   - return to triage
9. Pause for human review of the completed branch.
10. After explicit approval, squash to one final commit on the ticket branch.
11. Update Linear with evidence and next action.
12. Clean branch/worktree only after merge or explicit close.

## Bundled scripts

Use these scripts instead of hand-writing `git worktree` commands each time:

- `scripts/new-ticket-worktree.ps1`
- `scripts/remove-ticket-worktree.ps1`

Run them with `-WhatIf` first when validating the flow on a real ticket.

## Permission rules

Do not let workers:

- create or rename branches
- create commits or rewrite history
- run cross-ticket edits
- update critical Linear states
- merge into `main`
- clean another ticket's workspace

If a worker hits a boundary, return control to the orchestrator.

## Validation rules

Before moving a ticket forward, verify:

- changed files stay within ticket scope
- tests for touched behavior ran or an explicit gap is documented
- docs changed when a contract, decision, or workflow changed
- branch, worktree, and ticket ID still match

## Handoff contract

Every worker return should include:

- Linear ticket ID
- branch name
- worktree path
- files touched
- tests run
- open risks
- recommended next action

## Output format

When using this skill, report:

1. ticket selected
2. branch and worktree assigned
3. worker role delegated
4. validation status
5. Linear update or blocker
