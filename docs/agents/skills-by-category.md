# Skills by Category

> Human navigation index for `.agents/skills/` in this repo. Source of truth is each skill's `description:` field in `SKILL.md`. **Not auto-generated** — update this file when adding, removing, or renaming a skill. Vendored skills are tracked by `skills-lock.json` (synced from upstream `dotnet/skills`).
>
> **Agents:** this file is for humans. Your routing is done by the `available_skills` block in your system prompt, matched by `description:`. Do not load this file.

| Section | Count |
|---|---:|
| .NET runtime, libraries & app code | 13 |
| .NET testing infrastructure | 6 |
| MSBuild / build system | 15 |
| Test quality (polyglot) | 9 |
| Frontend / UI / design | 3 |
| Git & CI/CD | 3 |
| Process / engineering | 11 |
| Agent & skill creation | 3 |
| Communication | 1 |
| **Total top-level skills** | **64** |

---

## .NET runtime, libraries & app code

> Use when the request involves .NET runtime, libraries, ASP.NET Core, EF Core, observability, package management, or static-dependency migration. See `.agents/skills/<name>/SKILL.md` for the full procedure.

| Skill | Purpose |
|---|---|
| `analyzing-dotnet-performance` | Scans .NET code for ~50 performance anti-patterns across async, memory, strings, LINQ, regex, serialization, I/O. |
| `configuring-opentelemetry-dotnet` | OpenTelemetry tracing, metrics, and logging in ASP.NET Core via the .NET OTel SDK. |
| `convert-to-cpm` | Convert .NET solutions to NuGet Central Package Management (`Directory.Packages.props`). |
| `coverage-analysis` | Project-wide code coverage and CRAP score analysis for .NET projects; surfaces risk hotspots. |
| `crap-score` | Targeted CRAP score for a single .NET method, class, or file (Cobertura + cyclomatic complexity). |
| `detect-static-dependencies` | Scan C# for hard-to-test statics (`DateTime.Now`, `File.*`, `HttpClient`, `Environment.*`, etc.). |
| `dotnet-trace-collect` | Choose and run .NET diagnostic tools (`dotnet-trace`, `dotnet-dump`, `dotnet-counters`) for prod perf issues. |
| `dotnet-webapi` | ASP.NET Core Web API endpoints: HTTP semantics, OpenAPI metadata, global error handling. |
| `dump-collect` | Configure and collect crash dumps for modern .NET (CoreCLR, NativeAOT) in containers / k8s. |
| `find-untested-sources` | Parse-only C# analysis pairing source files with referencing tests; emits `source_to_tests` JSON. |
| `generate-testability-wrappers` | Generate `IFileSystem`, `IConsole`, `TimeProvider`, `IHttpClientFactory` wrappers for C# testability. |
| `migrate-static-to-wrapper` | Codemod-style bulk replacement of static calls to wrapper / `TimeProvider` within a scoped boundary. |
| `optimizing-ef-core-queries` | EF Core: N+1 fixes, tracking modes, compiled queries, common perf traps. |

## .NET testing infrastructure

> Use when the request involves running, migrating, or filtering .NET tests across VSTest ↔ MTP.

| Skill | Purpose |
|---|---|
| `run-tests` | `dotnet test` runner: detects VSTest vs MTP, picks matching command syntax and filter flags. |
| `platform-detection` | Reference data for detecting test platform (VSTest vs MTP) and framework from project files. |
| `mtp-hot-reload` | Use Microsoft Testing Platform (MTP) hot reload to iterate fixes on failing tests without rebuild. |
| `migrate-vstest-to-mtp` | Migrate .NET test projects from VSTest to Microsoft.Testing.Platform (MTP). |
| `dotnet-test-frameworks` | Reference: test framework detection, assertion APIs, skip / setup patterns for MSTest, xUnit, NUnit, TUnit. |
| `filter-syntax` | Reference: test filter syntax for VSTest (`--filter`) and MTP (`--filter-class` / `--filter-trait` / `--filter-query`). |

## MSBuild / build system

> Use when the request involves MSBuild infrastructure: build perf, antipatterns, item / property patterns, evaluation, parallelism, binlogs.

| Skill | Purpose |
|---|---|
| `binlog-failure-analysis` | Analyze MSBuild binary logs (binlogs) to diagnose cascading build failures. |
| `binlog-generation` | Add `/bl:{}` to `dotnet build / test / pack / publish / restore` to capture a full build trace. |
| `build-parallelism` | Optimize MSBuild parallelism: `/maxcpucount`, `/graph`, `BuildInParallel`, solution filters. |
| `build-perf-baseline` | Establish build perf baselines (cold / warm / no-op) and apply MSBuild Server, static graph, etc. |
| `build-perf-diagnostics` | Diagnose slow builds via binlog: RAR, Roslyn analyzers, target dominance, node utilization. |
| `check-bin-obj-clash` | Detect conflicting `OutputPath` / `IntermediateOutputPath` across projects / TFMs in binlog. |
| `directory-build-organization` | Organize MSBuild infrastructure with `Directory.Build.props / targets`, `Directory.Packages.props`, `.rsp`. |
| `eval-performance` | Diagnose slow MSBuild evaluation: glob patterns, deep imports, property functions with file I/O. |
| `extension-points` | MSBuild extensibility: `CustomBefore` / `CustomAfter` hooks, wildcard imports, NuGet package build layout. |
| `incremental-build` | Fix MSBuild incremental builds that re-execute unnecessarily (8 common causes). |
| `item-management` | MSBuild item group patterns: Include / Remove / Update, metadata, batching with `%(Metadata)`, transforms. |
| `msbuild-antipatterns` | Catalog of MSBuild anti-patterns with detection rules and BAD → GOOD fixes. |
| `msbuild-server` | Use `MSBUILDUSESERVER=1` to speed up CLI builds via persistent server-based caching. |
| `property-patterns` | MSBuild property definition patterns: conditional defaults, composition, path normalization, evaluation order. |
| `resolve-project-references` | Interpret `ResolveProjectReferences` time in MSBuild perf summaries; focus on task self-time. |

## Test quality (polyglot)

> Use when the request involves auditing, grading, generating, or analyzing tests in any language.

| Skill | Purpose |
|---|---|
| `assertion-quality` | Analyze variety and depth of assertions across test suites in any language. |
| `code-testing-agent` | Generate new unit tests for any language; scaffolds .NET, pytest, Vitest / Jest, Go, JUnit suites. |
| `code-testing-extensions` | Discover language-specific extension files (dotnet.md, cpp.md, etc.) for the code-testing pipeline. |
| `grade-tests` | Grade a specified set of test methods individually, A–F, with a one-line note each. |
| `test-analysis-extensions` | Discover language-specific reference files for the test ANALYSIS skills. |
| `test-anti-patterns` | Audit existing tests for anti-patterns; severity-ranked report (Critical / Warning / Info). |
| `test-gap-analysis` | Pseudo-mutation analysis on production code to find gaps in existing test suites. |
| `test-smell-detection` | Deep-dive audit using the testsmells.org 19-smell academic catalog with citable findings. |
| `test-tagging` | Tag tests with traits (positive, negative, boundary, smoke, regression, integration, perf, security). |

## Frontend / UI / design

> Use when the request involves designing, critiquing, or polishing a frontend interface.

| Skill | Purpose |
|---|---|
| `design-taste-frontend` | Anti-slop frontend for landing pages, portfolios, redesigns: real design systems, audit-first. |
| `impeccable` | Design / redesign / critique / polish / accessibility / motion / i18n for any frontend surface. |
| `minimalist-ui` | Clean editorial-style interfaces: warm monochrome, typographic contrast, flat bento grids. |

## Git & CI/CD

> Use when the request involves git branching, GitHub Actions workflow YAML, or Docker Compose.

| Skill | Purpose |
|---|---|
| `authoring-github-workflows` | Author and review GitHub Actions workflow YAML safely (actionlint-validated). |
| `docker-compose-context-hygiene` | Keep agent context small while working with Docker Compose: targeted status checks, bounded logs. |
| `git-no-ff-merge` | Enforce non-fast-forward merge workflow: fix / feature branch + Conventional Commits + `--no-ff`. |

## Process / engineering

> Use when the request involves TDD, diagnosis, code review, validation, handoff, triage, or repo-level navigation.

| Skill | Purpose |
|---|---|
| `clean-code` | Review and refactor code for readability, testability, structural cohesion (Clean Code + repo guardrails). |
| `diagnose` | Disciplined diagnosis loop for hard bugs and perf regressions: reproduce → minimize → hypothesize → fix. |
| `engineering-guardrails` | Apply repo engineering standards for contribution workflow, code quality, testability, error handling. |
| `grill-with-docs` | Grilling session that challenges a plan against the domain model and updates CONTEXT.md / ADRs inline. |
| `handoff` | Compact the current conversation into a handoff document for another agent to pick up. |
| `improve-codebase-architecture` | Find deepening opportunities, informed by `CONTEXT.md` and `docs/adr/`. |
| `local-validation` | Reproducible local validation: code checks, backend coverage, secret scanning, docker smoke tests. |
| `spec-change-map` | Route project changes to the right spec files; document the propagation path across ERS / ADRs / CONTEXT. |
| `tdd` | Test-driven development with red-green-refactor loop; integration tests; test-first development. |
| `triage` | Triage issues through a state machine driven by triage roles; create / review / prepare for AFK. |
| `zoom-out` | Zoom out and give broader context or a higher-level perspective for unfamiliar code. |

## Agent & skill creation

> Use when the request involves creating or orchestrating custom agents, skills, or Linear ticket workflows.

| Skill | Purpose |
|---|---|
| `create-custom-agent` | Create VS Code custom agent files (`.agent.md`) with tools, instructions, and handoffs. |
| `linear-ticket-orchestrator` | Orchestrate work from Linear tickets: one branch + one git worktree per ticket, authority boundaries. |
| `setup-matt-pocock-skills` | Set up the `## Agent skills` block in AGENTS.md / CLAUDE.md and `.agents/agents/`. |

## Communication

| Skill | Purpose |
|---|---|
| `caveman` | Ultra-compressed communication mode: ~75% token reduction by dropping filler, articles, pleasantries. |

---

## Sub-skills under `.agents/skills/tools/`

These are tool-specific invocations, not loaded into the main `available_skills` discovery. They live one level deeper.

| Path | Purpose |
|---|---|
| `.agents/skills/tools/grill-me/SKILL.md` | Interview-style grilling of a plan / design until shared understanding is reached. |
| `.agents/skills/tools/handoff/SKILL.md` | Companion to top-level `handoff` (file exists on disk but is not in the agent's `available_skills` list — verify). |
| `.agents/skills/tools/handoff/to-issues/SKILL.md` | Break a plan / spec / PRD into tracker issues using tracer-bullet vertical slices. |
| `.agents/skills/tools/to-prd/SKILL.md` | Turn the current conversation context into a PRD and publish it to the project issue tracker. |
| `.agents/skills/tools/write-a-skill/SKILL.md` | Create new agent skills with proper structure, progressive disclosure, and bundled resources. |

---

## Globally-available skills (not vendored in this repo)

These are loaded by opencode at session start but live outside this repo. Listed for human awareness; the source of truth is the file on disk.

| Skill | Location |
|---|---|
| `find-skills` | `~/.agents/skills/find-skills/SKILL.md` (user home) |
| `teach` | `~/.agents/skills/teach/SKILL.md` (user home) |
| `customize-opencode` | `<built-in>` (bundled with opencode, not on disk) |
