# CLAUDE.md — ButtonPanelTester

**Archetype:** A
**Standard version:** v1.2.1

This repo follows the STEM v1 standards documented in [`docs/Standards/`](./docs/Standards/) (inline copies pinned to the version above). Upstream source of truth lives in [`llm-settings`](https://github.com/luca-veronelli-stem/llm-settings/tree/v1.2.1/shared/standards) (private).

## Repo-specific notes

<!--
Anything that's not in the standards but is load-bearing for this repo.
Examples: vendor SDK that requires a specific runtime, hardware quirk,
non-default port for a development service, security exception.
-->

- **Peak PCAN-USB hardware required.** `Peak.PCANBasic.NET` (driver-only, Windows TFM) wraps a vendor SDK that needs a real Peak PCAN-USB adapter for end-to-end tests. CI runs without hardware, so E2E and most Integration tests are traited `Category=FlakyOnCi` and excluded from required CI runs.
- **Excel protocol dictionaries.** `Data` uses ClosedXML to read STEM protocol/variable dictionaries from `.xlsx` fixture files; the `-7155632` ARGB cell-color literal (see #28) is the tag the dictionary maintainers use to mark valid variable rows.
- **Mocking-library deviation from `dotnet.md`.** The test suite still uses `Moq` (9 files); manual-fakes migration is not yet scheduled — track separately from the F# migration.

## Language choices that deviate from defaults

<!--
Per LANGUAGE standard: each project that uses a non-default language
records a one-sentence justification here.
-->

- **All projects: C#** — predates the F# default; migration tracked under "Active migrations" below (Phase 2/3).

## Active migrations

- [x] Phase 1: structural adoption (v1.0 standards) — PR #1 — landed 2026-05-05.
- [x] v1.2.1 docs-standards alignment — PR #16 — landed 2026-05-06.
- [ ] Phase 2: F# migration of `Core` — not yet scheduled.
- [ ] Phase 3: F# migration of `Services` — not yet scheduled.
- [ ] Phase 4: Avalonia migration of `GUI.WinForms` → `GUI` — not yet scheduled (see #20 for try/catch hardening until then).
