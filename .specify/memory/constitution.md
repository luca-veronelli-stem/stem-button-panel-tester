<!--
SYNC IMPACT REPORT
==================
Version change: 1.0.0 → 1.0.1 (PATCH)
Bump rationale: Wording correction in Development Workflow. The original draft asserted
that the speckit `NNN-feature-name` numeric branch convention is "not used in this repo".
First contact with the speckit prerequisite scripts proved the assertion wrong: the scripts
gate path resolution on this convention, and working around them costs more than aligning.
This is a clarification, not a principle change — no semantic shift, no governance change.

Modified principles: (none)
Modified sections:
  - Development Workflow → "Branches" bullet now explicitly distinguishes general work
    branches (`feat/<desc>` etc.) from SDD branches under `/speckit-*`, which use
    `feat/NNN-feature-name` (the gitflow form spec-kit's scripts accept) and pair with
    `specs/NNN-feature-name/`.

Added principles: (none)
Added sections: (none)
Removed sections: (none)

Templates requiring updates:
  ⚠ .specify/templates/plan-template.md         — No change (gates unaffected by branch naming)
  ⚠ .specify/templates/tasks-template.md        — No change
  ⚠ .specify/templates/spec-template.md         — No change
  ⚠ .specify/templates/checklist-template.md    — No change
  ⚠ docs/Standards/*                              — No change

Follow-up TODOs: (none)

Prior version's Sync Impact Report (v1.0.0) — preserved for traceability:
  Version: (initial) → 1.0.0 (MAJOR; first ratified governance)
  Added principles: I Standards-First, II Layered Architecture, III Test-First with
    Hardware Stratification (all NON-NEGOTIABLE), IV Pragmatic .NET Defaults,
    V English by Default, VI Migration Discipline.
  Added sections: Domain Constraints, Development Workflow, Governance.
-->

# Stem.ButtonPanel.Tester Constitution

## Core Principles

### I. Standards-First (NON-NEGOTIABLE)

The repo MUST follow STEM v1.2.1 standards verbatim, as inlined under [`docs/Standards/`](../../docs/Standards/) and pinned in [`CLAUDE.md`](../../CLAUDE.md). Every plan, task, and implementation MUST cite the relevant standard when its rules apply, or declare an explicit deviation in `CLAUDE.md` "Repo-specific notes" with rationale and a tracking issue. Deviations are not silent.

**Rationale**: Cross-repo consistency is the whole point of the standards bundle. Ad-hoc divergence accumulates faster than it gets noticed. Concentrating deviations in one place makes the audit cheap and the eventual reconciliation tractable.

### II. Layered Architecture (NON-NEGOTIABLE)

Project dependencies follow the onion direction:

```
GUI.WinForms ──→ Services ──→ Communication ──→ Infrastructure ──→ Core
       │                                                              ▲
       └──→ Data ─────────────────────────────────────────────────────┘
```

`Core` MUST have no project dependencies. Upward dependencies (e.g., `Core → Services`) are forbidden. Skip-layer dependencies (e.g., `GUI → Communication`) are forbidden. `GUI.WinForms` is the composition root and the only project allowed to wire concrete implementations across layers. New cross-cutting concerns start at `Core` and propagate outward.

**Rationale**: Keeps `Core` swappable (F# migration, Phases 2–3) and `GUI` swappable (Avalonia migration, Phase 4) without churning the middle layers. Reverse-direction or skip-layer references silently couple migration phases together and force whole-repo rewrites.

### III. Test-First with Hardware Stratification (NON-NEGOTIABLE)

TDD applies to non-trivial behaviour: tests are written and demonstrably fail before the implementation lands. xUnit class and method naming MUST follow `{ClassName}Tests` + `{Method}_{Scenario}_{ExpectedResult}`. Tests requiring a Peak PCAN-USB adapter (or any other physical device) MUST be traited `[Trait("Category", "FlakyOnCi")]` and excluded from the required CI test run. Unit tests and host-only integration tests MUST run on CI without hardware.

**Rationale**: CI has no lab hardware, but hardware-dependent paths still need coverage when the lab is available. Stratification keeps the hardware-free CI green and the hardware-dependent suite explicit; without the trait, one flaky vendor SDK turn would silently hold up every merge.

### IV. Pragmatic .NET Defaults

- Manual DI in the composition root (`GUI.WinForms`); no DI container.
- Interfaces only when they have ≥2 implementations or cross a unit-test boundary.
- Manual fakes for collaborators in new tests. The existing 9 `Moq` files are a tracked deviation, not a precedent — see `CLAUDE.md`.
- `Nullable` is enabled everywhere; nullable annotations are part of the contract.
- Exceptions over null returns for unexpected states; null is reserved for "absent by design".
- Functions ≤15 LOC where practical; early returns over deep nesting; soft 100–110 / hard 120 column limit.

**Rationale**: A small team's bottleneck is reading code, not abstracting it. Interface explosion and DI-container indirection cost more here than they save. Manual fakes scale fine at the current test volume and never desync from the production type.

### V. English by Default

Identifiers, XML comments, GUI strings, commit messages, PR titles and descriptions, CHANGELOG entries, and Markdown docs (including specs, plans, tasks) MUST be in English. Italian is allowed only when explicitly requested for a specific artifact (e.g., a customer-facing GUI label that must be Italian). The `COMMENTS` standard's English-by-default rule supersedes legacy "Italian for All" patterns.

**Rationale**: Cross-repo searchability, onboarding, and AI-tool fluency outweigh local ergonomics. Mixed-language artifacts are the worst outcome — they erode searchability without delivering localization.

### VI. Migration Discipline

Active migrations are tracked in `CLAUDE.md` under "Active migrations" with PR and landing date. New code targets a migration's destination language/framework (F#, Avalonia) only while that migration's phase is `[ ]` and explicitly active in the list. Outside active phases, new code follows the current implementation choice for the affected project. Hybrid layers (half-F#, half-C#; mixed WinForms+Avalonia inside one project) are forbidden.

**Rationale**: A half-migrated layer costs more than a fully-legacy one — every reader pays the cognitive tax of switching mental models inside a single file or project. Phased commitment is the right primitive: migrate one project end-to-end before opening the next.

## Domain Constraints

- **Hardware coupling.** `Peak.PCANBasic.NET` wraps a vendor SDK that requires a real Peak PCAN-USB adapter for end-to-end CAN-bus paths. CI runs without hardware; affected tests are categorized per Principle III. The driver project is Windows-only (TFM `net10.0-windows`) per the `PORTABILITY` standard's "Windows-confined driver" pattern.
- **Excel protocol dictionaries.** `Data` reads STEM protocol/variable dictionaries from `.xlsx` fixtures via `ClosedXML`. The ARGB literal `-7155632` is the cell colour the dictionary maintainers use to mark valid variable rows; see issue #28 for provenance. Magic-number cleanup is tracked separately and MUST NOT be inlined into unrelated work.
- **Proprietary licence.** `LICENSE` is the STEM-internal proprietary EULA. Updates flow from the upstream template at `llm-settings/shared/templates/LICENSE.template`; never edit `LICENSE` for textual reasons here.
- **Standards inline copies are read-only.** `docs/Standards/*.md` is regenerated by `apply-repo-standard.ps1`. Edits there are lost on the next bump. Standards changes happen in `llm-settings`.

## Development Workflow

- **Branches.** General work branches: `feat/<short-description>`, `fix/<short-description>`, `refactor/<…>`, `docs/<…>`, `chore/<…>`, `test/<…>`. Spec-Driven Development branches paired with a `specs/NNN-feature-name/` directory MUST use the speckit-compatible form `feat/NNN-feature-name` (gitflow form: speckit's prerequisite scripts strip the leading segment and gate path resolution on the `NNN-` prefix). All branches are cut from `github/main`.
- **Commits.** Conventional Commits, lowercase after the colon, imperative mood, English body. One concern per commit. Use `git commit --fixup=<sha>` + `git rebase -i --autosquash` to consolidate before pushing — never accumulate "fix the previous commit" commits.
- **Pull Requests.** Open on **GitHub** via `gh pr create`. Never on Bitbucket. PR title in Conventional-Commits form; PR body explains *what* and *why*, plus alternatives considered. At least one of the labels `feat`, `fix`, `chore`, `docs`, `refactor`, `test`, `ci` is mandatory.
- **CI.** GitHub Actions is the CI of record. PRs MUST be green before merge (`gh pr checks <PR>`). Local pre-push: `dotnet build -c Release` and `dotnet test tests/Tests.csproj --framework net10.0`.
- **Merge strategy.** Rebase merge by default (linear history). Squash only when consolidating a noisy commit stream that no longer reflects the intended history.
- **Dual-remote.** `github` is the active remote; `bitbucket` is a mirror, kept in sync by `.github/workflows/mirror-bitbucket.yml` on every push to `main`. Direct pushes to `bitbucket` are blocked by `git remote set-url --push bitbucket no_push`.
- **Speckit phases.** `/speckit-constitution` (this file) → `/speckit-specify` → `[/speckit-clarify]` → `/speckit-plan` → `[/speckit-checklist]` → `/speckit-tasks` → `[/speckit-analyze]` → `/speckit-implement`. Each phase produces an artifact under `specs/<feature-name>/`. Optional phases are recommended for non-trivial features.

## Governance

- This constitution governs work inside `stem-button-panel-tester`. It defers to (a) the STEM v1.x standards in `docs/Standards/`, (b) the global rules in `~/.claude/rules/`, and (c) the upstream `llm-settings` repository for cross-repo conventions. When the constitution and a standard disagree, the constitution wins inside this repo and the disagreement is escalated to `llm-settings` as a separate PR.
- **Amendments** require a PR that updates this file with: (a) the new content, (b) a Sync Impact Report at the top of the file, (c) a `Last Amended` date bump, (d) a propagation review for `.specify/templates/*`, and (e) the constitutional reasoning in the PR description.
- **Versioning** follows semver: MAJOR for principle removal or backward-incompatible redefinition, MINOR for new principle/section or material expansion, PATCH for clarifications and wording fixes that preserve semantics.
- **Standard-version bumps** (e.g., `v1.2.1 → v1.3.0` of the STEM standards) flow through a separate `apply-repo-standard.ps1` PR. The constitution is updated only when the new standard introduces or removes a principle here.
- **Compliance.** Every `/speckit-plan` invocation MUST include a Constitution Check gate that explicitly addresses each principle (`I` through `VI`). Plans with unresolved gate violations MUST list them under "Complexity Tracking" with rationale; unjustified violations block `/speckit-tasks`.
- **Runtime guidance.** `CLAUDE.md` carries the project-specific notes (vendor quirks, deviations, active migrations). `docs/Standards/` carries the cross-repo standards. This constitution carries only the principles that govern *how this project plans and ships work*.

**Version**: 1.0.1 | **Ratified**: 2026-05-06 | **Last Amended**: 2026-05-06
