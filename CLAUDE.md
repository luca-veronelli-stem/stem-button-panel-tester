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
- **Dictionary — stopgap (credential + endpoint).** ⚠️ Three bypasses simultaneously: (1) DPAPI-backed credential store + first-run bootstrap are bypassed — API key is read directly from `Dictionary:ApiKey` (override via env var `Dictionary__ApiKey`; supplier-specific value lives in gitignored `appsettings.Production.json`); (2) wire-level credential sent as `X-Api-Key` instead of `Authorization: Bearer`; (3) endpoint is `GET /api/dictionaries/{DictionaryId}/resolved` (default id `2`, "Pulsantiere") instead of `GET /v1/dictionary`, with the response mapped to a single-`PanelType` `ButtonPanelDictionary` client-side. `Variable.Scaling` is hardcoded to `1.0`. Violates FR-011a/b/c, the contract in `specs/001-dictionary-from-api/contracts/dictionary-api.md`, and Constitution Principle I — full waiver in [`docs/STOPGAP_API_KEY.md`](./docs/STOPGAP_API_KEY.md). `DpapiCredentialStore.cs` and the `IInstallationCredentialStore` F# contract are retained on disk so the re-secure PR is a recompose, not a re-implement.

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
