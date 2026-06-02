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

1. Select the next ticket from the Linear saved view `Next Ticket ready for agent`.
   - If the view is unavailable, rebuild the same queue with `Team = Umbral Desarrollo`, `Label = ready-for-agent`, `Status in (Backlog, Todo)`, `Assignee = unassigned`, and blocked tickets excluded when dependency relations exist in Linear.
2. Read the Linear ticket, comments, labels, and acceptance criteria.
3. If context is incomplete, stop and return the ticket to `needs-info` or `needs-triage`.
4. If the ticket is executable, claim it from the orchestrator role.
5. Derive branch name:
   - `feature/<issue-id>-<slug>`
   - `fix/<issue-id>-<slug>`
   - `chore/<issue-id>-<slug>`
6. Create a dedicated `git worktree` for that branch.
7. Initialize runtime metadata and log files outside the branch content.
8. Dispatch the worker with:
   - ticket ID
   - exact worktree path
   - expected branch
   - acceptance criteria
   - bounded context
   - relevant ADRs
   - explicit do-not-cross boundaries
9. Require the worker to validate context with `assert-ticket-worktree.ps1` before editing.
10. If the worker cannot prove it is inside the assigned worktree, abort the run.
11. Receive worker output and run validation.
   - Use `.agents/skills/local-validation/` to choose the smallest repo validation command that still proves the ticket outcome.
12. Before integration, sync the ticket branch with `fetch` + `rebase` onto the remote integration branch instead of merging into local `develop` first.
13. Run one final repo-wide code gate after the rebase.
14. Decide next state:
   - keep moving toward PR
   - send back to human
   - split into follow-up tickets
   - return to triage
15. Pause for human review of the completed branch.
16. After explicit approval, squash to one final commit on the ticket branch.
17. Update Linear with evidence and next action.
18. Clean branch/worktree only after merge or explicit close.

## Bundled scripts

Use these scripts instead of hand-writing `git worktree` commands each time:

- `scripts/new-ticket-worktree.ps1`
- `scripts/initialize-ticket-runtime.ps1`
- `scripts/assert-ticket-worktree.ps1`
- `scripts/sync-ticket-branch.ps1`
- `scripts/remove-ticket-worktree.ps1`
- `scripts/write-ticket-worker-log.ps1`
- `scripts/watch-ticket-worker-log.ps1`

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
- runtime session metadata still points to the same worktree and branch

## Handoff contract

Every worker return should include:

- Linear ticket ID
- branch name
- worktree path
- runtime log path
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
