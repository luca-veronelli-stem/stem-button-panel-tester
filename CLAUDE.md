# CLAUDE.md — ButtonPanelTester

**Archetype:** A
**Standard version:** v1.2.1

This repo follows the STEM v1 standards documented in [`docs/Standards/`](./docs/Standards/) (inline copies pinned to the version above). Upstream source of truth lives in [`llm-settings`](https://github.com/luca-veronelli-stem/llm-settings/tree/v1.2.1/shared/standards) (private).

## Repo-specific notes

(Add anything that's not in the standards but is load-bearing for this repo. Examples: vendor SDK that requires a specific runtime, hardware quirk, non-default port for a development service, security exception.)

## Language choices that deviate from defaults

(Per LANGUAGE standard: each project that uses a non-default language records a one-sentence justification here.)

- _none yet_

<!--
Examples:
- `<App>.GUI.Windows`: C# — wraps a vendor's Win32 SDK whose generated bindings are C#-only.
- `<App>.LegacyImporter`: C# — predates the F# migration; planned for archetype migration phase 3.
-->

## Active migrations

(Track which phases of the MIGRATION standard are in flight for this repo.)

- [x] Structural adoption — PR #1 — landed 2026-05-05.
- [ ] Phase 2: F# migration of `<App>.Core` — issue #M — target YYYY-Q.
- [ ] Phase 3: F# migration of `<App>.Services` — issue #M.
- [ ] Phase 4: Avalonia migration of `<App>.GUI.Windows` → `<App>.GUI` — issue #M.
