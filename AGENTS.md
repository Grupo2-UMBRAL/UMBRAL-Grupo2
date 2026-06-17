# AGENTS.md — Entry Point & Navigation Map

> Point of entry for any agent working in this repository.
> This is a map, read only what you need when you need it (progressive disclosure).

---

## 1. Map of the Repository

| File / Folder | What it contains | When to read it |
| --- | --- | --- |
| `docs/product/ers.md` | ERS (Product Requirements) | To understand the domain and what to build |
| `docs/architecture/repo-structure.md` | Repo architecture | Before assuming folder structures |
| `src/*/CONTEXT.md` | Bounded context ubiquitous language | Before writing code in a specific service |
| `docs/architecture/adr/` | Architecture Decision Records | Before making hard-to-reverse changes |
| `.agents/agents/orchestrator.md` | Orchestrator workflow and Git rules | When taking tickets from Linear |
| `.agents/agents/operating-model.md` | Code guidelines, PR rules, TDD | Before writing, reviewing, or committing code |
| `.agents/agents/issue-tracker.md` | Linear issue lifecycle | When changing issue states |
| `.agents/skills/` | Specialized procedures (e.g., local-validation, tdd) | When asked or when facing a specific procedure |

## 2. Hard Rules (Non-negotiable)

- **One Ticket per Worktree**: Do not mix tickets. Each ticket MUST live in its own `git worktree` and branch (`feature/`, `fix/`, etc.).
- **Pre-Code Human Gate**: Never start writing implementation code without an approved Implementation Plan or Spec.
- **Strict TDD (Backend Only)**: Follow Red-Green-Refactor cycles strictly. Code must be verifiable via `Invoke-RepositoryValidation.ps1`. Frontend components will be tested via Playwright.
- **Mutation Testing**: A task is not `done` until it passes mutation testing constraints and local validation.
- **Orchestrator Absolute Authority**: Only the Orchestrator changes Linear status, creates branches, or handles `git` operations outside the worktree.
- **Worker Isolation**: Workers must log state in `.worktrees/_runtime/<ISSUE-ID>/session-state.md` and only act within their scoped worktree.
- **Domain Language**: Always use terminology from `CONTEXT.md`. Do not invent names.

## 3. Ponytail Rules (Lazy Senior Dev Mode)

Before writing any code, stop at the first rung that holds:
1. **YAGNI**: Does this need to be built at all? If no, skip it.
2. **Stdlib**: Does the standard library already do this? Use it.
3. **Native**: Does a native platform feature cover it? Use it.
4. **Installed Dependency**: Does an already-installed dependency solve it? Use it.
5. **One Line**: Can this be one line? Make it one line.
6. **Minimum**: Only then, write the minimum code that works.

Additional Rules:
- No abstractions that weren't explicitly requested.
- No new dependency if it can be avoided.
- Deletion over addition. Boring over clever. Fewest files possible.
- Mark intentional simplifications with a `ponytail:` comment.

