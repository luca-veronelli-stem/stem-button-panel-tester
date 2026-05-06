# Implementation Plan: Dictionary from stem-dictionaries-manager API

**Branch**: `feat/001-dictionary-from-api` | **Date**: 2026-05-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-dictionary-from-api/spec.md`

## Summary

Replace the Excel-fed button panel dictionary with `stem-dictionaries-manager`'s HTTP API as the authoritative source, with an at-rest JSON cache fallback for offline / unreachable-API operation.

**Technical approach**: introduce an `IDictionaryProvider` abstraction in `Core`. Two cooperating implementations live in `Infrastructure` — `HttpDictionaryClient` (live API) and `JsonFileDictionaryCache` (fallback). A `DictionaryService` in `Services` orchestrates the live-then-cache decision, holds the in-memory state, exposes the manual-refresh API, and drives the `DictionarySource` indicator that `GUI.WinForms` displays. Credentials are stored via DPAPI behind an `IInstallationCredentialStore` interface; the wire-level credential is a per-installation API key. **Provisioning mechanism for v1.0: per-supplier installer with embedded transport-encrypted bundle that unwraps to DPAPI on first run** (see research item R-1 for the rationale on not blocking on the bootstrap-exchange path). Excel-based dictionary loading is removed from runtime per FR-009; the existing `src/Data` Excel code is repurposed as test-fixture-only (research item R-7).

## Technical Context

**Language/Version**: **C# 13 + F# 9** on .NET 10. This feature **activates Phase 2 (`Core` → F#) and Phase 3 (`Services` → F#) in partial-active mode**: new Core types and the new Services orchestrator are F# in sibling projects (`src/Core.FSharp/`, `src/Services.FSharp/`); existing C# in `src/Core/` and `src/Services/` migrates per future PRs. Constitution VI forbids `.cs`+`.fs` inside one project, hence the sibling-project pattern. Infrastructure (HTTP, file IO, DPAPI) stays C# — those layers are not on a migration phase.
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
- **VI. Migration Discipline.** This feature is the beachhead for **Phase 2 (Core → F#) and Phase 3 (Services → F#)**, both transitioning from "not yet scheduled" to **partial-active**. CLAUDE.md "Active migrations" is updated in the same PR. New code: F# in `src/Core.FSharp/` and `src/Services.FSharp/`. Existing C# in `src/Core/` and `src/Services/` is unchanged — separate migration PRs port those types as features touch them. Constitution VI's "no hybrid inside one project" rule is preserved by the sibling-project pattern (`Core` ≠ `Core.FSharp` are distinct compilation units). Phase 4 (Avalonia) stays unscheduled; GUI ships WinForms. The Excel removal (FR-009) is in-scope per R-7. No hybrid layer introduced.

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
├── Core/                              EXISTING C# — unchanged by this feature; future Phase 2 PRs migrate types out
├── Core.FSharp/                       NEW F# project (Phase 2 beachhead; deps: none)
│   └── Dictionary/
│       ├── IDictionaryProvider.fs              (interface: FetchAsync ct → Task<DictionaryFetchResult>)
│       ├── IInstallationCredentialStore.fs     (interface: GetApiKeyAsync, SetApiKeyAsync, ClearAsync, GetInstallationAsync)
│       ├── DictionaryFetchResult.fs            (DU: Success of ButtonPanelDictionary*DateTimeOffset | Failed of FetchFailureReason*string option)
│       ├── FetchFailureReason.fs               (DU: NetworkUnreachable | Timeout | Unauthorized | MalformedPayload | ServerError | CacheAbsent | CacheUnreadable | SetupIncomplete)
│       ├── DictionarySource.fs                 (DU: Live of DateTimeOffset | Cached of DateTimeOffset*FetchFailureReason)
│       ├── Installation.fs                     (record: MachineName * UserSid * InstallationId)
│       └── CredentialLifecycleState.fs         (DU: Provisioned | Active | Rotated | Revoked | Expired)
│
├── Infrastructure/                    EXISTING C# (deps: Core, Core.FSharp)
│   └── Dictionary/                    NEW
│       ├── HttpDictionaryClient.cs           : IDictionaryProvider
│       ├── JsonFileDictionaryCache.cs        : IDictionaryProvider
│       ├── DpapiCredentialStore.cs           : IInstallationCredentialStore (Windows-only)
│       ├── DictionaryApiOptions.cs           (base URL, timeout 5s, MajorVersion="v1")
│       ├── DictionaryCacheEnvelope.cs        (on-disk JSON schema; internal to JsonFileDictionaryCache)
│       └── Dtos/DictionaryResponseDto.cs     (System.Text.Json DTO; mapped to F# domain types)
│
├── Communication/                     UNCHANGED
├── Data/                              EXISTING C# — runtime consumer removed (FR-009); project retained for test fixtures per R-7
│
├── Services/                          EXISTING C# — unchanged; future Phase 3 PRs migrate out
├── Services.FSharp/                   NEW F# project (Phase 3 beachhead; deps: Core, Core.FSharp, Communication)
│   └── Dictionary/
│       └── DictionaryService.fs                (orchestrator: fetch-or-cache, holds DictionarySource state,
│                                                exposes RefreshAsync(ct), coalesces in-flight requests)
│
└── GUI.WinForms/                      EXISTING C# (composition root; deps: all of the above)
    ├── Composition/
    │   └── ServiceComposition.cs             EDITED — wire IDictionaryProvider (Http+Cache),
    │                                          IInstallationCredentialStore (DPAPI), DictionaryService,
    │                                          DictionaryApiOptions
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

**Structure Decision**: introduce two new sibling F# projects (`Core.FSharp`, `Services.FSharp`) as Phase 2 / Phase 3 beachheads — this is the structural cost of activating those migrations. The new `Dictionary/` namespace lives in three layers (`Core.FSharp`, `Infrastructure`, `Services.FSharp`, plus tests) following the layered convention. Existing `Core` and `Services` C# projects are unchanged by this PR; future Phase 2/3 PRs port C# types into the F# siblings as features touch them. The Excel-reading `src/Data` project's runtime consumer is removed (FR-009); the project itself becomes test-fixture-only per R-7.

## Complexity Tracking

No principle violations to justify. Section is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_ | _(n/a)_ | _(n/a)_ |
