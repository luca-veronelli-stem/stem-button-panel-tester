# Research: Dictionary from stem-dictionaries-manager API

**Phase**: 0 — Outline & Research
**Date**: 2026-05-06
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Each research item resolves a HOW question that the spec deferred to plan, or a "best practices" question raised by a technology choice. Format per skill: **Decision** / **Rationale** / **Alternatives considered**.

---

## R-1 — Credential provisioning mechanism

**Decision**: **Per-supplier installer with embedded transport-encrypted bundle**, unwrapped to DPAPI on first run. Each supplier gets their own MSI; the MSI carries the supplier's API key encrypted with a transport key (one per build). On first run, the app reads the bundle, decrypts with the embedded transport key, re-encrypts via `ProtectedData.Protect(scope: CurrentUser)`, writes `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\credential.bin`, and erases the transport ciphertext from the install directory.

**Rationale**: The bootstrap-exchange path (`stem-dictionaries-manager#1`) is the strictly stronger long-term design but the endpoint does not exist yet — that ticket was just filed. Blocking this feature on a server-side change in a different repo against an undefined timeline is the wrong trade. The per-supplier installer path achieves all three FR-011 sub-requirements (encrypted-at-rest, no manual supplier setup, per-installation revocable: the supplier's key can be revoked server-side and the next API call will 401 → FR-011f) without coupling the rollout to `stem-dictionaries-manager`. When `#1` lands, we migrate the provisioning step in a follow-up; the runtime credential model (DPAPI + API-key-as-wire-credential per FR-011) is unchanged, so the migration is local to the install/first-run code.

**Alternatives considered**:
- *Bootstrap-exchange (rejected for v1 only)* — ideal on every axis except availability. Re-evaluated as a follow-up once `stem-dictionaries-manager#1` ships.
- *Per-supplier `appsettings.<supplier>.json` plaintext key* — what `stem-device-manager` currently does; rejected explicitly (the user's whole motivation for this work, and the reason `stem-device-manager#94` exists).
- *Embed key in binary (no installer parameterization)* — single key shared across all suppliers, no per-installation revocation. Rejected: violates FR-011c.
- *mTLS / client certificate per supplier* — strongest, but installing a client cert IS a "manual configuration step" without bespoke installer logic, and we're already accepting installer complexity for the API-key path. Net: equivalent installer complexity, weaker tooling support in `System.Net.Http`. Rejected.

**Forward-compatibility note**: when `stem-dictionaries-manager#1` lands, the migration is isolated:
1. Add a `BootstrapExchangeCredentialProvisioner` alongside the installer-bundle path in `Infrastructure`.
2. Switch the composition root to prefer bootstrap when available; fall back to installer-bundle for installations that pre-date the change.
3. Eventually retire the installer-bundle path.

---

## R-2 — DPAPI scope: `CurrentUser` vs `LocalMachine`

**Decision**: `DataProtectionScope.CurrentUser`.

**Rationale**: `CurrentUser` ties the encrypted blob to the Windows user account that wrote it — exactly the **Installation** identity unit defined in spec.md's Key Entities (one Windows user account on one workstation). `LocalMachine` would let any user on the machine decrypt the credential, which (a) breaks the "Installation = per-user-per-machine" invariant (CHK011 resolution) and (b) widens the attack surface unnecessarily on shared lab boxes.

**Alternatives considered**:
- *`LocalMachine`* — rejected for the reasons above. The only theoretical benefit (cache survives user-account changes) is moot because the dictionary cache itself is user-scoped per FR-013, so if the user account changes the cache is also unreachable.
- *Custom AES + key-derived-from-machine-fingerprint* — reinventing DPAPI poorly. DPAPI is the right tool.

---

## R-3 — HTTP client lifecycle

**Decision**: Single long-lived `HttpClient` instance owned by `HttpDictionaryClient`, constructed in the composition root and injected. Not `IHttpClientFactory`.

**Rationale**: `IHttpClientFactory` is the right answer in apps with many distinct clients targeting many endpoints. This app has exactly one consumer (`HttpDictionaryClient`) talking to exactly one host (`stem-dictionaries-manager`). The factory's main payoffs (named clients, transient handler pooling, Polly integration) are all addressing problems we don't have. A single `HttpClient` with `Timeout = 5s` (FR-012) and a `BaseAddress` set from `DictionaryApiOptions` is ~5 LOC and has no DNS-staleness risk because the app's lifetime per session is hours, not days, and DNS-bound services aren't on a typical supplier's roadmap.

**Alternatives considered**:
- *`IHttpClientFactory`* — overkill (see above). Reconsider if more API consumers land in this app.
- *New `HttpClient` per call* — anti-pattern (socket exhaustion, DNS resolution overhead). Rejected.

---

## R-4 — Resilience primitives: Polly vs hand-rolled

**Decision**: Hand-rolled. The full resilience contract is:
1. 5-second total timeout via `HttpClient.Timeout` (one config line).
2. No retry on 401 within session (FR-011f) — implemented as "if status == 401, surface auth-failure result, do not retry".
3. No retry on timeout/network failure — fall back to cache, log, done (FR-003 + no retry storm per FR-011d).
4. No circuit breaker — there's exactly one upstream and we don't multiplex.

**Rationale**: The above is ~30 LOC of straight-line C# inside `HttpDictionaryClient`. Adding `Polly` (a real dependency) for that doesn't earn its keep. Polly becomes attractive when retries with exponential backoff, jitter, bulkheading, or composable policies are needed — none of which the spec mandates.

**Alternatives considered**:
- *Polly with `WaitAndRetryAsync` + `CircuitBreakerAsync`* — adds a NuGet, more LOC than it saves, and the team currently has zero `Polly` usage to leverage existing familiarity. Reconsider when the second use case appears.

---

## R-5 — Sharing a `Stem.Auth.Bootstrap` library with `stem-device-manager`

**Decision**: **Defer**. Build the bundle/DPAPI path inline in `stem-button-panel-tester`'s `Infrastructure/Dictionary/` for v1.0. Once `stem-device-manager#94` reaches design phase, factor the common pieces (DPAPI wrapper, transport-key unwrap helper, bundle schema) into a `Stem.Security.InstallationCredentials` package in a separate repo or in `stem-communication`'s package set.

**Rationale**: Premature library extraction is a frequent footgun. We currently have one consumer (this app) and one design-phase consumer (`stem-device-manager#94`). Extracting before the second consumer's spec is concrete risks designing for an imagined future shape. Inline is cheap to write here and cheap to extract later (the surface is small: maybe 3 classes).

**Alternatives considered**:
- *Build the shared library now* — sequencing risk: blocks this feature on a separate package release; risks designing the wrong API.
- *Copy-paste between the two repos* — accumulates drift. Rejected as a permanent state, but acceptable for the brief v1 period before extraction.

---

## R-6 — Authentication header format

**Decision**: **`Authorization: Bearer <api-key>`**.

**Rationale**: `Bearer` is the modern de-facto standard for API-key authentication over HTTPS, well-supported by `HttpClient.DefaultRequestHeaders.Authorization` (`AuthenticationHeaderValue("Bearer", key)`), and trivially rotatable to a real OAuth bearer token if/when `stem-dictionaries-manager` migrates auth in a future major version. Using `Bearer` for a static API key is RFC-correct (RFC 6750 doesn't restrict the token's mint origin) and avoids inventing a custom `X-API-Key` convention that the team would maintain alone.

**Alternatives considered**:
- *Custom header `X-API-Key: <key>`* — works but bespoke. Doesn't compose with future OAuth migration. Rejected.
- *Query parameter `?api_key=<key>`* — credentials in URLs are a known anti-pattern (they end up in server access logs, browser history, etc.). Rejected.
- *HTTP Basic auth `Authorization: Basic base64(":<key>")`* — works, but `Basic` is overloaded with username:password semantics and confuses tooling. Rejected.

---

## R-7 — Excel project (`src/Data`) future

**Decision**: **Retain `src/Data` as test-fixture-only**, with all production-runtime references removed and the project moved out of the runtime dependency graph. Do not delete it — the existing Excel parsing logic is the reference implementation for the dictionary shape and is useful for property-based tests of `JsonFileDictionaryCache` (round-tripping known fixtures).

**Rationale**: SC-005 mandates "zero `.xlsx` reads at runtime in production" — a runtime path constraint, not a "delete the code" mandate. The Excel reader holds non-trivial knowledge (the `-7155632` ARGB literal, see #28; the column conventions; edge cases around malformed sheets). Keeping it under `src/Data` referenced only by test projects gives us a property-based oracle ("does the JSON cache produce the same `ButtonPanelDictionary` as the Excel reader for fixture X?") at near-zero maintenance cost. Full removal is a follow-up after the API path has been in production long enough that the fixture-equivalence value evaporates.

**Alternatives considered**:
- *Delete `src/Data` entirely now* — saves ~1 KB of `.slnx` weight; loses the equivalence-test oracle. Rejected: cost > benefit.
- *Move `src/Data` under `tests/`* — cleaner separation but breaks build-tooling assumptions about where projects live; not worth it.

---

## R-8 — JSON serialization: `System.Text.Json` vs `Newtonsoft.Json`

**Decision**: `System.Text.Json`.

**Rationale**: BCL, no extra NuGet, fast, supports source-generated serializers (`[JsonSerializable]`) which trim AOT-friendly. The repo has no existing `Newtonsoft.Json` codebase to harmonize with (verified via `find -name "*.csproj" | xargs grep -l Newtonsoft` — empty). Adopting the BCL default is the path of least resistance and matches the broader .NET 10 ecosystem direction.

**Alternatives considered**:
- *`Newtonsoft.Json`* — more permissive defaults, larger ecosystem of converters, but introduces a dependency we don't otherwise need. Rejected on YAGNI.

---

## R-9 — API path versioning

**Decision**: **Pin to `/v1/` major version path** for the steady-state dictionary call (`https://<host>/v1/dictionary`). Configure as `DictionaryApiOptions.MajorVersion = "v1"` so a future bump is config + code-aware, not a wholesale URL rewrite. The bootstrap-exchange call is **unversioned** (`https://<host>/register`) per `stem-dictionaries-manager#1`'s authoritative spec — see `contracts/register-api.md`. The version-mismatch between the two paths is intentional on the server side and tracked separately.

**Rationale**: Path-based versioning is the most discoverable, easiest to debug (URL alone tells you the contract version), and the most common convention in REST APIs. It survives any HTTP intermediary that strips headers. A breaking server-side change to `/v2/` is an explicit cutover rather than an implicit drift, which matches FR-010's "schema drift → fall back to cache" semantics: the v1 client gets a 404 from `/v2/dictionary` → fetch failure → cache fallback → log auth-distinct-from-network failure. Recovery is "ship a new client version pinned to `/v2/`".

**Alternatives considered**:
- *Header-based versioning (`Accept: application/vnd.stem.dictionaries.v1+json`)* — works, harder to debug from logs. Rejected.
- *Query-parameter versioning (`?api-version=1`)* — works, less idiomatic. Rejected.
- *No versioning, latest is implicit* — maximum coupling, worst failure mode. Rejected.

**Coordination**: this decision should be reflected in the contract document for `stem-dictionaries-manager`'s side (see `contracts/dictionary-api.md`); cross-link from `stem-dictionaries-manager#1` if the team there wants header versioning instead, that's their call and we'll update R-9.

---

## R-10 — `WireMock.Net` for integration tests

**Decision**: `WireMock.Net` for tests of `HttpDictionaryClient` against fake `stem-dictionaries-manager` responses. In-process, runs on Linux + Windows CI legs.

**Rationale**: Mature, well-maintained, runs in-process so tests are fast and don't require Docker or a separate test fixture. Supports request matching, response stubbing for the four cases we need to cover (200 happy path, 401, timeout, malformed body), and integrates naturally with xUnit fixtures.

**Alternatives considered**:
- *Custom `HttpMessageHandler` fake* — minimal dependencies, but reinvents the wheel for status-code/body stubbing; loses request-recording for assertions. Rejected.
- *`Microsoft.AspNetCore.TestHost`* — overkill, requires standing up an in-memory ASP.NET Core server. Rejected.
- *Real `stem-dictionaries-manager` instance in CI* — couples CI to another repo's deployment and breaks Constitution III's CI-without-hardware contract. Rejected.

---

---

## R-11 — F# adoption for new Core/Services types (Phase 2/3 partial-activation)

**Decision**: This feature **activates Phase 2 (Core → F#) and Phase 3 (Services → F#) in partial-active mode** per the user's directive that "this feature (and all subsequents) walks hand in hand with the MIGRATION plan." New Core types live in a new sibling project `src/Core.FSharp/`; the new `DictionaryService` orchestrator lives in `src/Services.FSharp/`. Existing C# code in `src/Core/` and `src/Services/` is untouched by this PR — separate Phase 2/3 PRs port C# types into the F# siblings as features touch them.

**Rationale**: 
1. **Standards adherence**. The `LANGUAGE` standard makes F# the default; CLAUDE.md tracks the C#-everywhere state as a deviation pending Phase 2/3. Adding new C# code now means writing-then-rewriting when migration formally begins. F# from the start avoids that double-spend.
2. **F# fits the domain.** `DictionaryFetchResult`, `DictionarySource`, `FetchFailureReason`, `CredentialLifecycleState` are textbook discriminated unions — awkward in C# (sealed class hierarchies, OneOf libraries) and natural in F#. Exhaustive matching closes a class of "forgot to handle the new variant" bugs that a C# enum + switch wouldn't.
3. **Constitution VI is preserved.** The "no hybrid layers inside one project" rule is honoured because `Core` and `Core.FSharp` are distinct compilation units; each project is mono-language. The hybrid is at the *layer* level (the Core layer now has both C# and F# code), which Constitution VI does not forbid — only intra-project mixing is forbidden. Sibling-project pattern is mechanically clean.
4. **Sets precedent for subsequent features**. The user explicitly said "all subsequents" walk with migration — meaning the next dozen features each add a few F# types or services. The sibling-project pattern absorbs that incremental work without churn. When Phases 2 and 3 reach completion (the C# `Core` and `Services` projects empty out), we delete those projects and rename the F# siblings.

**Alternatives considered**:
- *Stay all-C# for v1, do "F# preparation" by writing F#-friendly C# (records with `init`, sealed hierarchies).* Rejected: doesn't actually start the migration, just postpones it. Each subsequent feature would have to make this same call and the answer is the same — start now.
- *Rewrite all of `Core` to F# in this PR.* Rejected: massive scope creep, blocks the dictionary feature on a project-wide port. The whole point of Phase 2 being incremental is to avoid this.
- *Mix `.cs` and `.fs` files inside `Core.csproj` (multi-language project).* MSBuild supports it but Constitution VI forbids it ("hybrid layers inside one project"). And toolchain-wise it confuses IDEs and IL-merging. Rejected.
- *F# for Core but keep DictionaryService in C# for v1.* Considered. Rejected because Services is on Phase 3's path; the same logic that justifies F# in Core justifies it in the orchestrator. If we draw the line at Core, the next feature has to redraw it.

**What this means for downstream features**:
- New Core types → `Core.FSharp`. New Services orchestrators → `Services.FSharp`.
- Existing types that need to *change* — case-by-case: small clean-up edits stay in the C# project; a non-trivial rewrite migrates the type to the F# sibling and removes it from C# as a single PR (so the type doesn't exist twice). This keeps Phase 2/3 progress visible in the diff.
- Infrastructure stays C# (no migration phase covers it). GUI stays C#/WinForms (Phase 4 is Avalonia, separately gated and a much larger architectural shift; not implied by this directive).

**Update propagation**: CLAUDE.md "Active migrations" updated in the same PR; Phase 2/3 transition from `[ ] not yet scheduled` to `[~] partial-active` with the beachhead PR reference. The constitution itself does NOT change — Principle VI's wording already accommodates active-migration partial states.

---

## Open follow-ups (non-blocking)

These came up during research but don't block plan completion. Captured for `/speckit-tasks` or post-implementation:

- **Bootstrap-exchange migration** (after `stem-dictionaries-manager#1`): per R-1, follow-up PR; not v1 scope.
- **Library extraction** (after `stem-device-manager#94` reaches design): per R-5; not v1 scope.
- **Source-generated `[JsonSerializable]`** for cache + API DTOs: per R-8, an optimization that's free to add later. Skip in v1 unless cold-start measurements show JSON cost is meaningful.
- **AOT-readiness of DPAPI wrapper**: `System.Security.Cryptography.ProtectedData` is AOT-compatible per .NET 10 docs, but no AOT ship target for this app yet. No action needed.
