---
name: docker-compose-context-hygiene
description: Keeps agent context small while working with Docker Compose by preferring targeted status checks, bounded logs, runtime artifacts, and staged validation instead of streaming or dumping full output. Use when implementing, debugging, validating, or operating services through docker compose, especially during local stack bring-up, smoke tests, or multi-service failures.
---

# Docker Compose Context Hygiene

Use this skill when `docker compose` work can flood chat context.

## Goal

Keep active context small.
Move noisy evidence to runtime files.
Read only narrow slices back into chat.

## Quick start

1. Start with `docker compose config` or `docker compose ps`.
2. Use `logs --tail 80` for one service at a time.
3. Redirect bulky output into `.worktrees/_runtime/<TICKET>/`.
4. Summarize findings instead of pasting raw logs.

## Core rules

- Prefer `docker compose ps` before any logs.
- Prefer service-specific logs over all-service logs.
- Never dump full `docker compose logs` into chat.
- Prefer `up -d` over attached `up` unless live streaming is required.
- For long-running commands, write output to runtime files first.
- After each compose step, record short findings in `handoff.md` or `worker.log`.

## Standard workflow

1. Validate config
   - `docker compose --env-file .env.example -f docker-compose.dev.yml config`
2. Build or start in phases
   - infra first
   - backend services second
   - frontend last
3. Check state
   - `docker compose ps`
4. Inspect only broken service
   - `docker compose logs SERVICE --tail 80`
5. If output is noisy, redirect
   - `docker compose logs SERVICE --tail 200 > .worktrees/_runtime/<TICKET>/SERVICE.log`
6. Read back only tail or exact error lines
7. Write compact summary to runtime handoff

## Preferred command patterns

```powershell
docker compose ps
docker compose config
docker compose images
docker compose logs web --tail 80
docker compose logs edge-proxy --tail 80
docker compose exec web sh -lc "npm run lint"
docker compose exec web sh -lc "npm run build"
```

## Redirection patterns

```powershell
docker compose up -d *> .worktrees/_runtime/UMB-7/compose-up.txt
docker compose logs web --tail 200 > .worktrees/_runtime/UMB-7/web.log
docker compose ps > .worktrees/_runtime/UMB-7/compose-ps.txt
Get-Content .worktrees/_runtime/UMB-7/web.log -Tail 80
```

## Anti-patterns

- `docker compose logs` with no service filter
- `docker compose up` attached for many services when only final state matters
- repeating same long command output in chat
- solving from memory instead of checking `ps`, `config`, and targeted logs
- keeping evidence only in conversation instead of runtime files

## Handoff expectations

Always leave:

- command used
- service affected
- result summary
- artifact path if output was redirected
- next action

## Suggested runtime artifacts

- `.worktrees/_runtime/<TICKET>/compose-ps.txt`
- `.worktrees/_runtime/<TICKET>/compose-up.txt`
- `.worktrees/_runtime/<TICKET>/web.log`
- `.worktrees/_runtime/<TICKET>/edge-proxy.log`
- `.worktrees/_runtime/<TICKET>/handoff.md`

## Decision rule

If command output exceeds what fits in a short human summary, write it to a runtime file first, then report only:

- what failed
- where full output lives
- exact next step
