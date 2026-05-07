# CLAUDE.md — ButtonPanelTester

**Archetype:** A
**Standard version:** v1.3.2

This repo follows the STEM v1 standards documented in [`docs/Standards/`](./docs/Standards/) (inline copies pinned to the version above). Upstream source of truth lives in [`llm-settings`](https://github.com/luca-veronelli-stem/llm-settings/tree/v1.3.2/shared/standards) (private).

## Repo-specific notes

<!--
Anything that's not in the standards but is load-bearing for this repo.
Examples: vendor SDK that requires a specific runtime, hardware quirk,
non-default port for a development service, security exception.
-->

- **Peak PCAN-USB hardware required.** `Peak.PCANBasic.NET` (driver-only, Windows TFM) wraps a vendor SDK that needs a real Peak PCAN-USB adapter for end-to-end tests. CI runs without hardware, so E2E and most Integration tests are traited `Category=FlakyOnCi` and excluded from required CI runs.
- **Excel protocol dictionaries.** `Data` uses ClosedXML to read STEM protocol/variable dictionaries from `.xlsx` fixture files; the `-7155632` ARGB cell-color literal (see #28) is the tag the dictionary maintainers use to mark valid variable rows.
- **Mocking-library deviation from `dotnet.md`.** The test suite still uses `Moq` (9 files); manual-fakes migration is not yet scheduled — track separately from the F# migration.
- **License is a STEM-internal proprietary notice.** `LICENSE` is the same template used across STEM repos (upstream copy under `llm-settings/shared/templates/LICENSE.template`); update there when STEM settles on a definitive corporate template.
- **F# / C# interop notes.** Domain DUs in `Core.FSharp/Dictionary/` surface to C# as sealed class hierarchies (`DictionaryFetchResult.Success` / `.Failed` are nested types). `string voption` becomes `Microsoft.FSharp.Core.FSharpValueOption<string>` — awkward in C#, but acceptable per the data-model.md interop note. Use `FSharpValueOption<T>.NewValueSome(x)` / `.ValueNone` to construct, `.IsValueSome` / `.Value` to consume. F# modules with `[<RequireQualifiedAccess>]` (e.g. `Installation.installationsMatch`) compile to a class named `<Type>Module` from C#; prefer inlining trivial helpers on the C# side rather than crossing the boundary.
- **Dictionary credential bootstrap (`feat/001-dictionary-from-api`).** First-run unwraps the per-supplier API key from `appsettings.json:Dictionary:ApiKey` into the DPAPI store via `DictionaryCredentialBootstrap.EnsureAsync` in `Program.cs`. This is the developer-friendly path; the production installer-bundle path (R-1) replaces the config read with a transport-encrypted blob extraction at install time but keeps the same DPAPI runtime contract.

## Language choices that deviate from defaults

<!--
Per LANGUAGE standard: each project that uses a non-default language
records a one-sentence justification here.
-->

- **All projects: C#** — predates the F# default; migration tracked under "Active migrations" below (Phase 2/3).

## Active migrations

- [x] Phase 1: structural adoption (v1.0 standards) — PR #1 — landed 2026-05-05.
- [x] v1.2.1 docs-standards alignment — PR #16 — landed 2026-05-06.
- [~] Phase 2: F# migration of `Core` — **partial-active** since `feat/001-dictionary-from-api`. New Core types land in `src/Core.FSharp/` (F#); existing C# types in `src/Core/` migrate per individual PRs as features touch them. Constitution VI's "no hybrid inside one project" rule is preserved by using sibling projects, not mixed `.cs` + `.fs` in `Core`. Beachhead: `feat/001-dictionary-from-api`.
- [~] Phase 3: F# migration of `Services` — **partial-active** since `feat/001-dictionary-from-api`. New orchestration code lands in `src/Services.FSharp/` (F#); existing C# services migrate per individual PRs. Beachhead: `feat/001-dictionary-from-api`.
- [ ] Phase 4: Avalonia migration of `GUI.WinForms` → `GUI` — not yet scheduled (see #20 for try/catch hardening until then).
