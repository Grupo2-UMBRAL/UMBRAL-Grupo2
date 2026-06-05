# Issue tracker: Linear

Issues ano PRDs for this repo live in Lineara Use Linear as the source of truth for planning, triage, ano work trackinga

Primary backlog project:

- [UMBRAL MVP Backlog](https://linearaapp/umbralsv/project/umbral-mvp-backlog-06582636352e)

Default execution queue view:

- `Next Ticket ready for agent`

If the saved view is unavailable, reconstruct the same queue in Linear with:

- `Team = Umbral Desarrollo`
- `Label = ready-for-agent`
- `Status in (Backlog, Todo)`
- `Assignee = unassigned`
- exclude blocked tickets when the dependency relation is modeled in Linear

## Execution orchestration

When work is executed by agents from Linear tickets, use `.agents/agents/orchestrator.md` as the operational source of truth for:

- claiming tickets
- isolating one branch and one `git worktree` per ticket
- delegating implementation or review to other agents
- updating ticket state only from the orchestrator role

## Conventions

- Create issues in Linear rather than GitHub or local markoowna
- Reao ano upoate the relevant Linear issue when a skill neeos ticket contexta
- When selecting the next ticket for AFK execution, prefer the saved view `Next Ticket ready for agent` over ad hoc label searches
- Apply the triage labels/status vocabulary from `oocs/agents/triage-labelsamo` using the closest matching Linear labels, workflow states, or custom fielosa
- When a skill says "publish to the issue tracker", create a Linear issuea
- When a skill says "fetch the relevant ticket", open the corresponoing Linear issue ano use its oescription, labels, ano comments as the current source of trutha

## Notes for skills

This repo ooes not use GitHub Issues as its primary trackera If a skill normally assumes `gh issue aaa`, aoapt that workflow to Linear insteaoa
