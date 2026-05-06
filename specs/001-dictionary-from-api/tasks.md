---

description: "Task list for feature implementation"
---

# Tasks: Dictionary from stem-dictionaries-manager API

**Input**: Design documents from `/specs/001-dictionary-from-api/`
**Prerequisites**: [`spec.md`](./spec.md), [`plan.md`](./plan.md), [`research.md`](./research.md), [`data-model.md`](./data-model.md), [`contracts/`](./contracts/)

**Tests**: REQUIRED per Constitution III (Test-First with Hardware Stratification). Each test task identifies whether it runs host-only on CI or carries `[Trait("Category", "WindowsOnly")]` (Linux CI leg filters it out via `--filter "Category!=WindowsOnly"`).

**Organization**: tasks are grouped by user story (US1, US2, US3) so each story can be implemented and tested independently. Phase 2 (Foundational) blocks all stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependencies between them)
- **[Story]**: which user story or shared phase the task belongs to (`Setup`, `Foundational`, `US1`, `US2`, `US3`, `Polish`)
- File paths are absolute under repo root.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: scaffolding shared by all user stories. New F# projects, new directories, NuGet wiring. No production code yet.

- [X] T001 [Setup] Create `src/Core.FSharp/Core.FSharp.fsproj` (Phase 2 beachhead). Inherit from `Directory.Build.props`. Target `net10.0`. No project references (Core is the deps-free layer). Add to `Stem.ButtonPanel.Tester.slnx` under the `src` folder.
- [X] T002 [Setup] Create `src/Services.FSharp/Services.FSharp.fsproj` (Phase 3 beachhead). Inherit from `Directory.Build.props`. Project references: `Core`, `Core.FSharp`, `Communication`. Add to `.slnx`.
- [X] T003 [Setup] Add `Core.FSharp` as a project reference from `Infrastructure.csproj`, `Services.FSharp.fsproj`, `GUI.WinForms.csproj`, and `tests/Tests/Tests.csproj`.
- [X] T004 [Setup] Add `Services.FSharp` as a project reference from `GUI.WinForms.csproj` and `tests/Tests/Tests.csproj`.
- [X] T005 [P] [Setup] Add `WireMock.Net` (test-only NuGet) to `Directory.Packages.proj` (CPM) and reference from `tests/Tests/Tests.csproj`. Pin a recent stable version.
- [X] T006 [P] [Setup] Add `System.Security.Cryptography.ProtectedData` (runtime NuGet) to `Directory.Packages.proj` and reference from `Infrastructure.csproj`.
- [X] T007 [P] [Setup] Create directory `src/Infrastructure/Dictionary/` with a placeholder `_Readme.md` so Git tracks it.
- [X] T008 [P] [Setup] Create test directory tree under `tests/Tests/`: `Core/Dictionary/`, `Infrastructure/Dictionary/`, `Services/Dictionary/`, `Integration/Dictionary/`. Placeholder `_Readme.md` per directory.
- [X] T009 [Setup] Update CLAUDE.md "Active migrations" to mark Phase 2/3 as `[~] partial-active`. **Note**: this edit is already in the in-flight commit that lands plan.md; T009 verifies it landed correctly and is consistent with the new project structure.

**Checkpoint**: solution builds with two empty new F# projects and one empty Dictionary namespace in Infrastructure. CI green.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: F# domain types and interfaces that EVERY user story depends on. No story can begin until this phase completes.

**⚠️ CRITICAL**: tests below are written and demonstrably failing before the corresponding implementations land (Constitution III).

### Tests for Foundational (REQUIRED per Constitution III) ⚠️

> Write these tests FIRST. All host-only on CI (no DPAPI yet at this layer).

- [X] T010 [P] [Foundational] Property test: `Installation` records with the same `(MachineName, UserSid, InstallationId)` are equal under F# `=`; differing `InstallationId` makes records unequal under full equality but the boundary helper `installationsMatch` (used by `DpapiCredentialStore.GetInstallationAsync`) returns true on `(MachineName, UserSid)` match alone. File: `tests/Tests/Core/Dictionary/InstallationTests.fs` (xUnit + FsCheck).
- [X] T011 [P] [Foundational] Test: `DictionaryFetchResult` exhaustive pattern match over all `FetchFailureReason` values compiles without warnings; a deliberate "missing case" stub triggers FS0025 at compile time. File: `tests/Tests/Core/Dictionary/DictionaryFetchResultTests.fs`.
- [X] T012 [P] [Foundational] Test: `DictionarySource` state-transition invariants per [data-model.md](./data-model.md): `Live → Live` on successful refresh, `Cached → Live` on successful refresh, refresh failure while Live keeps the existing `Live` (does not regress to `Cached` mid-session). File: `tests/Tests/Core/Dictionary/DictionarySourceTests.fs`.

### Implementation for Foundational

- [X] T015 [Foundational] Implement `FetchFailureReason` DU in `src/Core.FSharp/Dictionary/FetchFailureReason.fs` per data-model.md. 8 variants, no payloads.
- [X] T016 [Foundational] Implement `DictionaryFetchResult` DU in `src/Core.FSharp/Dictionary/DictionaryFetchResult.fs`. Variants `Success of ButtonPanelDictionary * DateTimeOffset` and `Failed of FetchFailureReason * string option`. (Depends on T015 for `FetchFailureReason`.)
- [X] T017 [Foundational] Implement `DictionarySource` DU in `src/Core.FSharp/Dictionary/DictionarySource.fs`. Variants `Live of DateTimeOffset` and `Cached of DateTimeOffset * FetchFailureReason`. (Depends on T015.)
- [X] T018 [Foundational] Implement `Installation` record in `src/Core.FSharp/Dictionary/Installation.fs` with `MachineName: string`, `UserSid: string`, `InstallationId: Guid`. Add `installationsMatch` helper that compares only `(MachineName, UserSid)` per CHK011/data-model.md.
- [X] T019 [Foundational] Implement `CredentialLifecycleState` DU in `src/Core.FSharp/Dictionary/CredentialLifecycleState.fs`. 5 variants per FR-011e.
- [X] T020 [Foundational] Define `IDictionaryProvider` interface in `src/Core.FSharp/Dictionary/IDictionaryProvider.fs` per data-model.md. Single member: `FetchAsync: CancellationToken -> Task<DictionaryFetchResult>`.
- [X] T021 [Foundational] Define `IInstallationCredentialStore` interface in `src/Core.FSharp/Dictionary/IInstallationCredentialStore.fs`. Members: `GetApiKeyAsync`, `SetApiKeyAsync`, `ClearAsync`, `GetInstallationAsync`. ValueOption return types per data-model.md.
- [X] T022 [Foundational] Add C# `DictionaryApiOptions` in `src/Infrastructure/Dictionary/DictionaryApiOptions.cs`. Fields: `BaseUrl: Uri`, `MajorVersion: string` (default `"v1"`), `Timeout: TimeSpan` (default 5s per FR-012). Add `IValidateOptions<DictionaryApiOptions>` validator: BaseUrl must be absolute HTTPS, MajorVersion must match `^v\d+$`, Timeout must be > 0 and ≤ 30s. (Kept C# because `IOptions<T>` validators are friendlier in C#.)

**Checkpoint**: Foundation ready. F# domain types compile, tests T010-T012 pass. User story implementation can now begin in parallel.

---

## Phase 3: User Story 1 — Live API fetch (Priority: P1) 🎯 MVP-half

**Goal**: app fetches the dictionary from `stem-dictionaries-manager` on startup; on success, an in-memory `ButtonPanelDictionary` is available with `DictionarySource = Live(now)`. No cache yet, no GUI changes yet — MVP ships only after US2 lands.

**Independent Test**: spin up `WireMock.Net` returning the `200 OK` body from [contracts/dictionary-api.md](./contracts/dictionary-api.md), instantiate `HttpDictionaryClient` with a hard-coded credential, call `FetchAsync(ct)`, assert `Success(dict, fetchedAt)` with `dict` matching the stub's payload.

### Tests for US1 (REQUIRED per Constitution III) ⚠️

> Write these tests FIRST. All host-only on CI (Linux + Windows legs both run them).

- [ ] T030 [P] [US1] Test: happy path — `WireMock.Net` returns the success body from [contracts/dictionary-api.md](./contracts/dictionary-api.md); `HttpDictionaryClient.FetchAsync(ct)` returns `Success(dict, fetchedAt)` with all `panel_types` deserialized into the existing `ButtonPanelDictionary` shape. File: `tests/Tests/Infrastructure/Dictionary/HttpDictionaryClientHappyPathTests.cs`.
- [ ] T031 [P] [US1] Test: 401 → `Failed(Unauthorized, _)`. WireMock stub returns 401; client does NOT retry within the call; the `Authorization: Bearer` header was sent on the first attempt. File: `tests/Tests/Infrastructure/Dictionary/HttpDictionaryClientAuthTests.cs`.
- [ ] T032 [P] [US1] Test: timeout → `Failed(Timeout, _)`. WireMock stub delays the response past the configured 5s timeout; assertion: result is `Failed(Timeout, _)` and total elapsed < 6s (timeout enforced). File: `tests/Tests/Infrastructure/Dictionary/HttpDictionaryClientTimeoutTests.cs`.
- [ ] T033 [P] [US1] Test: malformed payload → `Failed(MalformedPayload, _)`. WireMock returns 200 with: (a) truncated JSON, (b) `schema_version` ≠ 1, (c) empty `panel_types: []`. Three sub-cases, three result assertions. File: `tests/Tests/Infrastructure/Dictionary/HttpDictionaryClientMalformedTests.cs`.
- [ ] T034 [P] [US1] Test: network unreachable → `Failed(NetworkUnreachable, _)`. Point client at a closed port (TCP RST); assertion: result is `Failed(NetworkUnreachable, _)` and elapsed < 1s (no timeout fall-through). File: `tests/Tests/Infrastructure/Dictionary/HttpDictionaryClientNetworkTests.cs`.
- [ ] T035 [P] [US1] Test: 5xx → `Failed(ServerError, _)`. Distinct from `NetworkUnreachable` in that the server was reachable. File: `tests/Tests/Infrastructure/Dictionary/HttpDictionaryClientServerErrorTests.cs`.
- [ ] T036 [P] [US1] Test: cancellation — caller cancels `CancellationToken` mid-flight; client returns promptly; outcome is observed cancellation, not a `Failed(Timeout)`. File: `tests/Tests/Infrastructure/Dictionary/HttpDictionaryClientCancellationTests.cs`.

### Implementation for US1

- [ ] T040 [US1] Implement DTO `DictionaryResponseDto` in `src/Infrastructure/Dictionary/Dtos/DictionaryResponseDto.cs` matching the response body in [contracts/dictionary-api.md](./contracts/dictionary-api.md). Use `[JsonRequired]` on mandatory fields. Mapper `ToDomain(): ButtonPanelDictionary` lives next to the DTO.
- [ ] T041 [US1] Implement `HttpDictionaryClient : IDictionaryProvider` in `src/Infrastructure/Dictionary/HttpDictionaryClient.cs`. Constructor takes `HttpClient` (single long-lived per R-3), `IInstallationCredentialStore`, `IOptions<DictionaryApiOptions>`, `ILogger<HttpDictionaryClient>`. `FetchAsync` flow: read API key, set `Authorization: Bearer`, GET `{BaseUrl}/{MajorVersion}/dictionary`, branch on outcome → `DictionaryFetchResult`. Logging per FR-014 (distinct levels for auth-failure vs network vs malformed).
- [ ] T042 [US1] Wire `HttpDictionaryClient` into `GUI.WinForms/Composition/ServiceComposition.cs` with the URL pulled from `appsettings.json` via `IOptions<DictionaryApiOptions>`. Note: NO real credential yet at this checkpoint — composition uses a placeholder `IInstallationCredentialStore` returning a hardcoded test key, replaced by `DpapiCredentialStore` in US2.

**Checkpoint**: US1 alone is verifiable end-to-end against `WireMock.Net` and against a real `stem-dictionaries-manager` once an API key is in hand. Not shippable on its own (would regress vs Excel — no offline operation).

---

## Phase 4: User Story 2 — Cache fallback (Priority: P1) MVP-completion

**Goal**: when the live fetch fails, the app uses the most recently cached dictionary so suppliers in the field with intermittent connectivity can keep working. Plus: real DPAPI-backed credential store. Together with US1, this completes the MVP.

**Independent Test**: write a valid cache file with `JsonFileDictionaryCache.WriteAsync` after a fake successful fetch; bring the API down (or close the WireMock harness); restart the app; assert `DictionarySource = Cached(prevTimestamp, NetworkUnreachable)` and the dictionary content matches what was cached.

### Tests for US2 (REQUIRED per Constitution III) ⚠️

> Write these tests FIRST. DPAPI tests are `[Trait("Category", "WindowsOnly")]`. Cache file tests use `Path.GetTempPath()` so they're cross-platform. Service tests use F# manual fakes (no Moq).

#### Cache file tests (host-only)

- [ ] T050 [P] [US2] Test: round-trip — write envelope, read it back, deserialized `ButtonPanelDictionary` equals the input. File: `tests/Tests/Infrastructure/Dictionary/JsonFileDictionaryCacheRoundtripTests.cs`.
- [ ] T051 [P] [US2] Test: atomic-write semantics. Inject a fault that crashes after `.tmp` write but before rename; assert the previous good cache is intact and `.tmp` is left in place (cleanup is the cache's responsibility on next read or via explicit purge). FR-002. File: `tests/Tests/Infrastructure/Dictionary/JsonFileDictionaryCacheAtomicTests.cs`.
- [ ] T052 [P] [US2] Test: schema drift — cache with `schema_version: 2` triggers `Failed(CacheUnreadable, _)`; file is left in place (not deleted). FR-010. File: `tests/Tests/Infrastructure/Dictionary/JsonFileDictionaryCacheSchemaDriftTests.cs`.
- [ ] T053 [P] [US2] Test: corruption — malformed JSON triggers `Failed(CacheUnreadable, _)`; truncated file triggers same. FR-010. File: `tests/Tests/Infrastructure/Dictionary/JsonFileDictionaryCacheCorruptionTests.cs`.
- [ ] T054 [P] [US2] Test: concurrent writes from two threads against the same path; final file contains one writer's complete payload (no torn bytes); both threads observe success. FR-002. File: `tests/Tests/Infrastructure/Dictionary/JsonFileDictionaryCacheConcurrencyTests.cs`.
- [ ] T055 [P] [US2] Test: cache absent — first call against a clean directory returns `Failed(CacheAbsent, _)`. File: `tests/Tests/Infrastructure/Dictionary/JsonFileDictionaryCacheAbsentTests.cs`.

#### DPAPI credential store tests (`[Trait("Category", "WindowsOnly")]`)

- [ ] T056 [P] [US2] Test: round-trip — `SetApiKeyAsync(key)` then `GetApiKeyAsync()` returns `ValueSome(key)`. WindowsOnly. File: `tests/Tests/Infrastructure/Dictionary/DpapiCredentialStoreRoundtripTests.cs`.
- [ ] T057 [P] [US2] Test: clear — `ClearAsync` overwrites the file with random bytes before deleting; subsequent `GetApiKeyAsync` returns `ValueNone`. Defense-in-depth check: forensic recovery of the deleted file does not yield the prior cleartext. WindowsOnly. File: `tests/Tests/Infrastructure/Dictionary/DpapiCredentialStoreClearTests.cs`.
- [ ] T058 [P] [US2] Test: installation mismatch — write a credential, simulate a `MachineName` change in the stored Installation, then call `GetApiKeyAsync`; result is `ValueNone` (auto-cleared) and the credential file is gone. CHK007. WindowsOnly. File: `tests/Tests/Infrastructure/Dictionary/DpapiCredentialStoreInstallationMismatchTests.cs`.

#### Service orchestration tests (host-only, F# with manual fakes)

- [ ] T059 [P] [US2] Test: live fetch succeeds → state Live, cache written. Manual fake `IDictionaryProvider` returns `Success`; capturing fake `IDictionaryProvider` (cache role) records what was written. File: `tests/Tests/Services/Dictionary/DictionaryServiceLiveSuccessTests.fs`.
- [ ] T060 [P] [US2] Test: live fails (each `FetchFailureReason` except `CacheAbsent`/`CacheUnreadable`/`SetupIncomplete`), cache present → state Cached(reason). Parameterized over reasons. File: `tests/Tests/Services/Dictionary/DictionaryServiceFallbackTests.fs`.
- [ ] T061 [P] [US2] Test: live fails + cache absent → typed result `NoDictionaryAvailable` (FR-008); state remains uninitialized; the caller (GUI) bubbles to the modal error. File: `tests/Tests/Services/Dictionary/DictionaryServiceNoDictionaryTests.fs`.
- [ ] T062 [P] [US2] Test: setup incomplete — `IInstallationCredentialStore.GetApiKeyAsync` returns `ValueNone` → live fetch produces `Failed(SetupIncomplete, _)`; service surfaces FR-011d's distinct error path. File: `tests/Tests/Services/Dictionary/DictionaryServiceSetupIncompleteTests.fs`.

#### Integration test (host-only, end-to-end)

- [ ] T063 [P] [US2] Integration: `WireMock.Net` + temp filesystem. Cold start (no cache, no credential) → SetupIncomplete error path. Then prime credential, restart → live fetch succeeds, cache written. Then break WireMock, restart → cached state, distinct UI/log signal. Maps to quickstart.md scenarios 1, 2, 4. File: `tests/Tests/Integration/Dictionary/DictionaryEndToEndTests.cs`.

### Implementation for US2

- [ ] T070 [US2] Implement `DictionaryCacheEnvelope` DTO in `src/Infrastructure/Dictionary/DictionaryCacheEnvelope.cs` per data-model.md (`schema_version`, `fetched_at`, `dictionary` payload). System.Text.Json with `[JsonRequired]`.
- [ ] T071 [US2] Implement `JsonFileDictionaryCache : IDictionaryProvider` in `src/Infrastructure/Dictionary/JsonFileDictionaryCache.cs`. Constructor takes `IOptions<DictionaryCacheOptions>` (path), `ILogger<JsonFileDictionaryCache>`. `FetchAsync` reads + validates envelope, returns `Success` or `Failed(CacheAbsent|CacheUnreadable, _)`. `WriteAsync(dict, fetchedAt)` does atomic write: write to `.tmp`, fsync, `File.Move(..., overwrite: true)`. (Depends on T070.)
- [ ] T072 [US2] Implement `DpapiCredentialStore : IInstallationCredentialStore` in `src/Infrastructure/Dictionary/DpapiCredentialStore.cs`. Uses `ProtectedData.Protect/Unprotect` with `DataProtectionScope.CurrentUser` per R-2. Stores `{ apiKey, installation }` JSON DPAPI-blob at `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\credential.bin`. `ClearAsync` overwrites with random bytes before delete (T057 contract).
- [ ] T073 [US2] Implement F# `DictionaryService` in `src/Services.FSharp/Dictionary/DictionaryService.fs`. State held in a private `ref` cell (or `MailboxProcessor` for thread-safe coordination per CHK021). Public surface: `Initialize(): Task<DictionarySource>`, `RefreshAsync(ct): Task<DictionarySource>`, `Source: IObservable<DictionarySource>` (or event for WinForms-friendliness). Composes the two `IDictionaryProvider` impls (live, cache) per the orchestration logic in data-model.md state diagram. (Depends on T015-T021 Foundational.)
- [ ] T074 [US2] In `DictionaryService`: implement coalesce-or-reject behaviour for in-flight `RefreshAsync` invocations (CHK021). Suggested: `MailboxProcessor` accepting `Refresh` messages, single in-flight at a time. (Depends on T073.)
- [ ] T075 [US2] Wire `DpapiCredentialStore`, `JsonFileDictionaryCache`, `DictionaryService` into `GUI.WinForms/Composition/ServiceComposition.cs`, replacing the placeholder credential store from T042. `IDictionaryProvider` registered as keyed services: `"live"` → `HttpDictionaryClient`, `"cache"` → `JsonFileDictionaryCache`. `DictionaryService` resolves both by key.

**Checkpoint**: MVP (US1 + US2) complete. App handles full happy-path + offline-fallback + setup-incomplete paths. No GUI indicator yet — that's US3. Internal demo viable from this checkpoint.

---

## Phase 5: User Story 3 — Manual refresh + indicator (Priority: P2)

**Goal**: technicians see whether they're working off a live or cached dictionary, with the cache age, and can manually refresh without restarting the app.

**Independent Test**: with the app running on a cached dictionary (US2 fallback path), bring the API back online, click the GUI Refresh button; assert the indicator transitions Cached → Live within ≤5s.

### Tests for US3 (REQUIRED per Constitution III) ⚠️

> Host-only. F# manual fakes for the orchestrator tests; no Moq.

- [ ] T080 [P] [US3] Test: `Cached → Live` transition on `RefreshAsync` success — the `Source` event fires with the new `Live(now)` and the cache is overwritten. File: `tests/Tests/Services/Dictionary/DictionaryServiceRefreshCachedToLiveTests.fs`.
- [ ] T081 [P] [US3] Test: `Live → Live` (timestamp updated) on `RefreshAsync` success while already Live. File: `tests/Tests/Services/Dictionary/DictionaryServiceRefreshLiveToLiveTests.fs`.
- [ ] T082 [P] [US3] Test: `RefreshAsync` failure while Live retains the prior `Live` state (no regression to `Cached` mid-session, per data-model.md). The new failure is logged but the state event does NOT fire. File: `tests/Tests/Services/Dictionary/DictionaryServiceRefreshLiveFailureTests.fs`.
- [ ] T083 [P] [US3] Test: concurrent `RefreshAsync` calls — second call coalesces with the first (returns the same `Task<DictionarySource>`) OR returns `RefreshInProgress` synchronously. CHK021. File: `tests/Tests/Services/Dictionary/DictionaryServiceConcurrentRefreshTests.fs`.

### Implementation for US3

- [ ] T090 [US3] In `src/GUI.WinForms/MainForm.cs`: add a `dictionarySourceLabel` (Label control) bound to `DictionaryService.Source`. Distinct visual states for `Live`, `Cached(NetworkUnreachable)`, `Cached(Unauthorized)`, `Cached(other)`, plus a transient "Refreshing…" state during in-flight requests (FR-005, FR-011f distinct UI). Strings in English; localization is a separate pass.
- [ ] T091 [US3] In `src/GUI.WinForms/MainForm.cs`: add a `refreshDictionaryButton` (Button control), visible from main view per FR-006. Click handler: call `DictionaryService.RefreshAsync(ct)` with `ct` bound to form `FormClosed` event. Disable button while refresh is in-flight; re-enable on completion.
- [ ] T092 [US3] In `src/GUI.WinForms/MainForm.cs`: hook async-void `try/catch` discipline per `ERROR_HANDLING` standard + open issue #20. Any exception escaping the click handler is logged via `ILogger` and surfaced as a non-fatal toast; never crash the form.

**Checkpoint**: all three user stories functional. Feature is shippable to internal demo and, after Polish, to suppliers.

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: removal of legacy paths, documentation, the leftover items from the security-resilience checklist, and verification.

- [ ] T100 [P] [Polish] Remove every runtime reference to `src/Data` from `src/`. The Excel reader code stays in `src/Data` but no `src/` project depends on it (verify with `dotnet sln ... list-references` / `.slnx` audit). Closes FR-009 / SC-005.
- [ ] T101 [P] [Polish] Address the 17 remaining items in [`checklists/security-resilience.md`](./checklists/security-resilience.md). Most resolve to small wording tightenings on existing FRs or one-liner edits; treat any that have aged into "high-impact" during implementation as additional spec/test work.
- [ ] T102 [P] [Polish] CHK022/CHK023 measurement: define "configuration step" precisely enough that a CI test can assert FR-011b — e.g. "no environment variables consulted, no UI prompt during install, no file the supplier edits". Consider a static-analysis check or a small custom analyser.
- [ ] T103 [P] [Polish] Update `CHANGELOG.md` `[Unreleased]` section with the dictionary-from-api feature: user-visible behaviour change, new `Core.FSharp` and `Services.FSharp` projects, cross-link `stem-device-manager#94` and `stem-dictionaries-manager#1`.
- [ ] T104 [P] [Polish] Update `CLAUDE.md` "Repo-specific notes" if the new F# projects need any callouts beyond the migration tracker entry from T009 (e.g. F# tooling versions or build flags worth documenting).
- [ ] T105 [Polish] Run all five quickstart.md scenarios manually on a real Windows workstation. Record each scenario's outcome in the PR description as a checklist.
- [ ] T106 [P] [Polish] Source-generated `[JsonSerializable]` for `DictionaryResponseDto` and `DictionaryCacheEnvelope` if cold-start measurements show JSON cost is meaningful (R-8 follow-up). **SKIP** if not measured to be a problem.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** — no deps. Start immediately.
- **Phase 2 (Foundational)** — depends on Setup; **BLOCKS all user stories**.
- **Phase 3 (US1)** — depends on Foundational. Can proceed in parallel with Phase 4 once Foundational is done.
- **Phase 4 (US2)** — depends on Foundational. The DictionaryService orchestrator (T073-T075) depends on `HttpDictionaryClient` from US1 being importable, but the cache + DPAPI implementations (T070-T072) are independent of US1.
- **Phase 5 (US3)** — depends on US1 + US2 (the indicator transitions and refresh action both need orchestrated state).
- **Phase 6 (Polish)** — depends on all user stories.

### User-story dependencies

- **US1** (live fetch): independent after Foundational.
- **US2** (cache fallback): independent after Foundational, **MVP requires both** because either alone regresses vs Excel (US1 alone = no offline; US2 alone = can never refresh).
- **US3** (indicator + refresh): depends on US1 + US2.

### Within each user story

- Tests written and demonstrably failing **before** implementation.
- F# types compile before the C# code that consumes them — observe project order in `.slnx` / build output.
- Models/types before services; services before GUI.

### Parallel opportunities

- All `[P]` tasks within a phase are different files / different test classes.
- Setup tasks T005-T008 run in parallel.
- Foundational implementation tasks T015-T021 are different `.fs` files.
- Once Foundational lands, US1 (T030-T042) and US2's non-orchestrator tasks (T050-T072) can proceed in parallel.

---

## Implementation Strategy

### MVP first (US1 + US2 = MVP)

1. Phase 1 Setup — half a day.
2. Phase 2 Foundational — one day for F# types, interfaces, and their tests. Land before any user-story PR opens.
3. Phase 3 US1 — one day for `HttpDictionaryClient` + full test matrix.
4. Phase 4 US2 — two days for cache + DPAPI store + `DictionaryService` orchestration + tests.
5. **STOP and validate**: run quickstart.md scenarios 1-4. Bring a real dictionary into the cache. Confirm offline operation and FR-008 first-run-offline error. **Internal demo to STEM team** before any supplier-facing work.

### Incremental delivery

1. Setup + Foundational → foundation ready (single PR, "feat(plan-phase-1-2): Core.FSharp + Services.FSharp scaffolding + foundational types").
2. + US1 → live-API-only build. NOT shipped externally — regression vs Excel.
3. + US2 → MVP. Internal-demo-ready.
4. + US3 → suppliers see indicator + refresh control. Shippable.
5. + Polish → production-ready.

### Avoid (per task ordering and constitution)

- F# project not yet present when C# project tries to reference it — observe `.slnx` ordering on first build.
- Refresh button before `DictionaryService` exists.
- ANY `Moq` usage in new tests (Constitution IV). Manual fakes only — F# manual fakes for F# interfaces, C# manual fakes for C# code.
- Mixing `.fs` and `.cs` files inside the same project — new F# code lives in `Core.FSharp` / `Services.FSharp`, NEVER in `Core` / `Services` (Constitution VI no-hybrid-inside-one-project rule).
- Touching the existing C# `Core` or `Services` projects in this PR — Phase 2/3 migration of *existing* types is a separate decision tracked under "Active migrations".

---

## Notes

- `[P]` = different file, no shared dependency.
- `[Story]` = `Setup`, `Foundational`, `US1`, `US2`, `US3`, `Polish`.
- F# tests use xUnit + FsCheck per repo convention. C# tests use xUnit + manual fakes.
- DPAPI tests carry `[Trait("Category", "WindowsOnly")]`. Linux CI leg filters via `--filter "Category!=WindowsOnly"`. Windows CI leg runs the full suite.
- This feature is the first that activates Phase 2 (`Core.FSharp`) and Phase 3 (`Services.FSharp`). Subsequent features extend those projects rather than creating more.
- CLAUDE.md "Active migrations" must reflect Phase 2/3 partial-active before this PR merges (T009).
