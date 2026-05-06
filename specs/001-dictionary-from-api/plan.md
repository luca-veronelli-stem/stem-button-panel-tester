# Implementation Plan: Dictionary from stem-dictionaries-manager API

**Branch**: `feat/001-dictionary-from-api` | **Date**: 2026-05-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-dictionary-from-api/spec.md`

## Summary

Replace the Excel-fed button panel dictionary with `stem-dictionaries-manager`'s HTTP API as the authoritative source, with an at-rest JSON cache fallback for offline / unreachable-API operation.

**Technical approach**: introduce an `IDictionaryProvider` abstraction in `Core`. Two cooperating implementations live in `Infrastructure` — `HttpDictionaryClient` (live API) and `JsonFileDictionaryCache` (fallback). A `DictionaryService` in `Services` orchestrates the live-then-cache decision, holds the in-memory state, exposes the manual-refresh API, and drives the `DictionarySource` indicator that `GUI.WinForms` displays. Credentials are stored via DPAPI behind an `IInstallationCredentialStore` interface; the wire-level credential is a per-installation API key. **Provisioning mechanism for v1.0: per-supplier installer with embedded transport-encrypted bundle that unwraps to DPAPI on first run** (see research item R-1 for the rationale on not blocking on the bootstrap-exchange path). Excel-based dictionary loading is removed from runtime per FR-009; the existing `src/Data` Excel code is repurposed as test-fixture-only (research item R-7).

## Technical Context

**Language/Version**: C# 13 / .NET 10. F# is reserved for Phase 2 (`Core`) and Phase 3 (`Services`) per CLAUDE.md "Active migrations"; both are unscheduled, so this feature ships C#.
**Primary Dependencies**:
- `System.Net.Http` (BCL) — HTTP client.
- `System.Text.Json` (BCL) — JSON serialization for cache and API payloads.
- `System.Security.Cryptography.ProtectedData` (NuGet) — DPAPI wrapper for the credential store.
- `Microsoft.Extensions.Logging.Abstractions` — `ILogger<T>` per `LOGGING` standard.
- `Microsoft.Extensions.Options` — strongly-typed config (`DictionaryApiOptions`).
- `WireMock.Net` (test-only) — fake `stem-dictionaries-manager` for integration tests.

No `Polly`. The spec's resilience contract (single 5s timeout per FR-012, no in-session retry per FR-011f, no retry storm per FR-011d) is hand-rolled in <30 LOC and doesn't justify a dependency.

**Storage**:
- Dictionary cache: `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\dictionary.json` (per-Installation, atomic write per FR-002).
- Credential: `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\credential.bin` (DPAPI-protected, scope `CurrentUser`, per FR-011a).

**Testing**: xUnit + FsCheck (per repo convention). Manual fakes for `IDictionaryProvider` and `IInstallationCredentialStore`; no `Moq` in new tests (Constitution IV; the existing 9 `Moq` files are tracked deviation, not precedent). `WireMock.Net` for integration tests of `HttpDictionaryClient`. DPAPI tests are platform-bound — traited `[Trait("Category", "WindowsOnly")]` and excluded from the Linux CI leg via `--filter "Category!=WindowsOnly"`.

**Target Platform**: Windows 10/11 desktop, .NET 10 runtime. The existing GUI is WinForms; Avalonia migration (Phase 4) is unscheduled.

**Project Type**: Desktop app (Archetype A).

**Performance Goals**: Dictionary load ≤5s on healthy network (SC-004), ≤1s from cache (SC-004), atomic cache write (FR-002). Dictionary size assumed ≤1 MB JSON (~hundreds of variables across panel types) — fits comfortably in memory; no streaming required.

**Constraints**:
- SC-005: zero `.xlsx` reads at runtime in production builds.
- FR-007: full panel-testing functionality available offline once a valid cache exists.
- FR-013: forward-compatible cache path (per-environment subdirectory addable later without breaking existing installations).

**Scale/Scope**: Single user per Installation, single `stem-dictionaries-manager` environment in v1, a few tens of supplier installations expected.

## Constitution Check

*GATE: Reference: [`.specify/memory/constitution.md`](../../.specify/memory/constitution.md) v1.0.1.*

- **I. Standards-First.** Applies: `LANGUAGE` (C# deviation tracked in CLAUDE.md), `PORTABILITY` (DPAPI is Windows-confined, lives in a dedicated class behind `IInstallationCredentialStore` so the rest of `Infrastructure` stays cross-platform), `TESTING` (xUnit + manual fakes), `LOGGING` (ILogger; FR-014 distinct levels), `THREAD_SAFETY` (concurrent cache writes per FR-002), `CANCELLATION` (HTTP fetches accept `CancellationToken` for the manual-refresh button), `CONFIGURATION` (`IOptions<DictionaryApiOptions>` for base URL + timeout; secret never in config). `ERROR_HANDLING`: exceptions for unexpected internal states (e.g. corrupt DPAPI blob); the API/cache failure modes already named in FRs are domain returns, not exceptions. **No deviations.**
- **II. Layered Architecture.** Touched projects: `Core` (new `Dictionary/` namespace — interfaces + entities), `Infrastructure` (new `Dictionary/` namespace — HTTP client, JSON file cache, DPAPI store), `Services` (new `Dictionary/` — orchestrator), `GUI.WinForms` (composition root + main form indicator + refresh button). `Communication` and `Data` not touched at runtime; `Data` may be deprecated as a project (research R-7). No upward dependencies, no skip-layer dependencies (`GUI` does not call `Infrastructure` directly — only via `Services`). Composition root remains `GUI.WinForms`.
- **III. Test-First with Hardware Stratification.** Test seams: `IDictionaryProvider` (manual fake for orchestration tests), `IInstallationCredentialStore` (manual fake for HTTP-client tests so they don't need DPAPI). Host-only on CI: orchestration tests, JSON-cache tests, HTTP-client tests against `WireMock.Net`. **Windows-only, runs on the windows-latest CI leg only**: `DpapiCredentialStore` tests (`Category=WindowsOnly`). **No `Category=FlakyOnCi` tests** added — this feature does not touch Peak PCAN-USB. Tests are written before implementation per the upcoming `tasks.md` ordering.
- **IV. Pragmatic .NET Defaults.** New interfaces: `IDictionaryProvider` (2 prod impls + 1 fake), `IInstallationCredentialStore` (1 prod + 1 fake; crosses unit-test boundary). Both earn their keep. No interface for `DictionaryService` (single implementation; if we later need to fake it for GUI tests, add then). No `Moq`. Composition root in `GUI.WinForms` only. ✅
- **V. English by Default.** All new identifiers, comments, log messages, and the four new user-visible strings (FR-005 indicator label, FR-006 refresh button, FR-008 / FR-011d error messages) are English. No supplier-side request for Italian — if it surfaces later, treat as a localization pass against resource files.
- **VI. Migration Discipline.** Phases 2/3/4 (F#, Avalonia) are not active per CLAUDE.md. This feature ships C# / WinForms end-to-end. The Excel removal (FR-009) is in-scope; the `src/Data` Excel project is either retained for test fixtures or removed entirely (R-7). No hybrid layer introduced.

**No principle violations. Constitution Check passes.**

## Project Structure

### Documentation (this feature)

```text
specs/001-dictionary-from-api/
├── spec.md                # /speckit-specify + /speckit-clarify output
├── plan.md                # this file
├── research.md            # Phase 0 — decisions and rationale
├── data-model.md          # Phase 1 — entity shapes and state transitions
├── quickstart.md          # Phase 1 — manual verification recipe
├── contracts/             # Phase 1 — consumer contract for stem-dictionaries-manager
│   ├── dictionary-api.md      # GET /dictionary expected shape + error codes
│   └── register-api.md        # /register bootstrap exchange (cross-link to stem-dictionaries-manager#1)
├── checklists/
│   └── security-resilience.md # /speckit-checklist output
└── tasks.md               # Phase 2 — /speckit-tasks output (NOT created here)
```

### Source Code (repository root)

```text
src/
├── Core/                              (no project deps)
│   └── Dictionary/                    NEW
│       ├── IDictionaryProvider.cs
│       ├── IInstallationCredentialStore.cs
│       ├── DictionaryFetchResult.cs   (sealed class hierarchy: Success | Failed)
│       ├── DictionarySource.cs        (sealed: Live | Cached(timestamp))
│       ├── Installation.cs
│       └── CredentialLifecycleState.cs (enum: Provisioned/Active/Rotated/Revoked/Expired)
│
├── Infrastructure/                    (deps: Core)
│   └── Dictionary/                    NEW
│       ├── HttpDictionaryClient.cs           : IDictionaryProvider
│       ├── JsonFileDictionaryCache.cs        : IDictionaryProvider
│       ├── DpapiCredentialStore.cs           : IInstallationCredentialStore (Windows-only)
│       ├── DictionaryApiOptions.cs           (base URL, timeout 5s)
│       └── DictionaryCacheEnvelope.cs        (on-disk schema: payload + fetched_at + schema_version)
│
├── Communication/                     UNCHANGED
├── Data/                              EXISTING — Excel reader; runtime consumer removed; project either retained for test fixtures or removed entirely (R-7)
│
├── Services/                          (deps: Communication, Core)
│   └── Dictionary/                    NEW
│       └── DictionaryService.cs              (orchestrator: fetch-or-cache, holds DictionarySource state, exposes RefreshAsync())
│
└── GUI.WinForms/                      (composition root)
    ├── Composition/
    │   └── ServiceComposition.cs             EDITED — wire IDictionaryProvider, IInstallationCredentialStore, DictionaryService
    └── MainForm.cs                            EDITED — DictionarySource label, Refresh button (FR-005, FR-006)

tests/Tests/
├── Core/Dictionary/                   NEW — entity tests (Installation equality, DictionarySource exhaustive)
├── Infrastructure/Dictionary/         NEW
│   ├── JsonFileDictionaryCacheTests.cs       (atomic write, schema drift, corruption)
│   ├── HttpDictionaryClientTests.cs          (against WireMock.Net: 200/401/timeout/malformed)
│   └── DpapiCredentialStoreTests.cs          (Category=WindowsOnly)
├── Services/Dictionary/               NEW
│   └── DictionaryServiceTests.cs             (orchestration via manual fakes)
└── Integration/Dictionary/            NEW
    └── DictionaryEndToEndTests.cs            (WireMock + filesystem; cold start, cache hit, fallback)
```

**Structure Decision**: extend existing projects; no new top-level projects required. The new `Dictionary/` namespace appears in four projects (`Core`, `Infrastructure`, `Services`, plus tests) following the layered convention. The Excel-reading `src/Data` project's runtime consumer is removed (FR-009); the project itself either becomes test-fixture-only (zero references from `src/`) or is removed entirely depending on R-7's outcome.

## Complexity Tracking

No principle violations to justify. Section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_ | _(n/a)_ | _(n/a)_ |
