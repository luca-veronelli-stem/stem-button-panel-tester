# Data Model: Dictionary from stem-dictionaries-manager API

**Phase**: 1 — Design & Contracts
**Date**: 2026-05-06
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

This document specifies the entities, fields, relationships, validation rules, and state transitions introduced by this feature. Implementation lives under `src/Core.FSharp/Dictionary/` (F# domain types and interfaces — Phase 2 beachhead) and `src/Infrastructure/Dictionary/` (C# implementations and the on-disk envelope DTO). F# discriminated unions and records are the natural expression for the polymorphic and immutable shapes below; the C# layers consume them via standard CLR interop (DUs surface as sealed class hierarchies, records as immutable classes with structural equality).

## Entities

### `ButtonPanelDictionary` (already exists; reused)

Domain type representing the loaded dictionary. **Shape unchanged by this feature** — the source changes (API or cache instead of `.xlsx`), the contract does not. Existing consumers in `Communication`, `Services`, and `GUI.WinForms` see the same type.

Implementation note: confirm during `/speckit-tasks` that the existing `ButtonPanelDictionary` in `Core` doesn't depend on Excel-specific types (it shouldn't, but verify — issue #21 explicitly tracks purging `System.Drawing` and WinForms types from Core contracts).

### `DictionaryFetchResult` (new, in `Core.FSharp/Dictionary/`)

F# discriminated union representing the outcome of a single fetch attempt — what `HttpDictionaryClient` and `JsonFileDictionaryCache` both return. The DU shape gives exhaustive matching at compile time, which closes a class of bugs (forgetting to handle a new failure reason) that a C# enum + switch would not.

```fsharp
type DictionaryFetchResult =
    | Success of Dictionary: ButtonPanelDictionary * FetchedAt: DateTimeOffset
    | Failed  of Reason: FetchFailureReason * Detail: string option
```

| Variant | Fields | Meaning |
|---|---|---|
| `Success` | `Dictionary: ButtonPanelDictionary`, `FetchedAt: DateTimeOffset` | A valid dictionary was obtained. `FetchedAt` is the wall-clock time when the response completed (for live) or when the cache was originally written (for cache). |
| `Failed` | `Reason: FetchFailureReason`, `Detail: string option` | The fetch did not yield a dictionary. `Reason` is the discriminator; `Detail` is an optional human-readable elaboration for logging. |

`FetchFailureReason` (F# DU, no payload):

```fsharp
type FetchFailureReason =
    | NetworkUnreachable
    | Timeout
    | Unauthorized
    | MalformedPayload
    | ServerError
    | CacheAbsent
    | CacheUnreadable
    | SetupIncomplete
```

| Value | When |
|---|---|
| `NetworkUnreachable` | DNS, TCP, TLS handshake failure; no HTTP status received. |
| `Timeout` | The 5-second `HttpClient.Timeout` (FR-012) was exceeded. |
| `Unauthorized` | HTTP 401 received. Triggers FR-011f handling. |
| `MalformedPayload` | HTTP 200 received but body did not deserialize, was empty, or failed schema validation (US2 acceptance scenario 2). |
| `ServerError` | HTTP 5xx — distinct from `NetworkUnreachable` (server reachable but failing). |
| `CacheAbsent` | Cache provider only — no cache file on disk. |
| `CacheUnreadable` | Cache provider only — file exists but is unreadable (corruption, schema drift; FR-010). |
| `SetupIncomplete` | First-launch path: no credential available because bootstrap/installer-bundle has not yet succeeded. Triggers FR-011d handling. |

### `DictionarySource` (new, in `Core.FSharp/Dictionary/`)

F# discriminated union representing the origin of the **active** in-memory dictionary. Drives FR-005's UI indicator. Exhaustive matching ensures the GUI's indicator code can't silently miss a state.

```fsharp
type DictionarySource =
    | Live   of FetchedAt: DateTimeOffset
    | Cached of FetchedAt: DateTimeOffset * FallbackReason: FetchFailureReason
```

| Variant | Fields | Meaning |
|---|---|---|
| `Live` | `FetchedAt: DateTimeOffset` | The active dictionary came from a successful API fetch in this session. |
| `Cached` | `FetchedAt: DateTimeOffset`, `FallbackReason: FetchFailureReason` | The active dictionary came from the cache because the live fetch failed. `FallbackReason` carries the why so FR-014 logging and FR-005 UI can distinguish "API down" from "credential issue" (CHK017 resolution). |

State transitions (managed by `DictionaryService`):

```
                                     ┌─── live fetch succeeds ─→ Live(now)
                                     │
   (no active dictionary yet) ───────┤
                                     │
                                     └─── live fetch fails ─────→ Cached(cache.fetched_at, reason)
                                                                      │
                                                                      └── manual refresh succeeds ─→ Live(now)
                                                                      └── manual refresh fails ────→ Cached unchanged (keeps prior FallbackReason; logs new failure)
```

The transition `Cached → Live` is the only state machine event the GUI displays prominently (FR-005, FR-006). The reverse (`Live → Cached`) does not happen mid-session by design — once the session has a Live dictionary, it stays Live until the app closes or the user manually refreshes and the refresh fails (in which case we keep the old Live dictionary; FR-011f says auth failure surfaces as "credential problem" but does NOT discard the in-memory dictionary — that would be a regression).

Correction to the state machine above: **`Live → Live`** on manual refresh success; **`Live → Live (with warning)`** on manual refresh failure (the existing Live dictionary stays, and a transient warning surfaces). The `Live → Cached` transition does NOT occur mid-session; it only occurs at the next launch's startup fetch if that fetch fails.

### `DictionaryCacheEnvelope` (new, in `Infrastructure/Dictionary/`)

The on-disk JSON envelope at `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\dictionary.json`. This is the persistence format; it is internal to `JsonFileDictionaryCache` and not exposed to other layers.

```json
{
  "schema_version": 1,
  "fetched_at": "2026-05-06T11:23:45.123+02:00",
  "dictionary": {
    /* ButtonPanelDictionary serialized payload */
  }
}
```

Fields:
- `schema_version` (int, required) — increments on incompatible envelope changes. v1 = current shape. On read, `JsonFileDictionaryCache` checks `schema_version == 1`; any other value triggers `FetchFailureReason.CacheUnreadable` per FR-010.
- `fetched_at` (ISO 8601 with offset, required) — wall-clock at successful API response, recorded by the writer; surfaced as `DictionarySource.Cached.FetchedAt` and `DictionarySource.Live.FetchedAt`.
- `dictionary` (object, required) — `ButtonPanelDictionary` serialized via `System.Text.Json`. Nested shape is the existing domain type.

**Atomic write** (FR-002): write to `dictionary.json.tmp` in the same directory, `fsync`, then `File.Move(..., overwrite: true)` to `dictionary.json`. Concurrent writers race only on the rename, which is atomic on NTFS — the loser's bytes are immediately discarded by the OS, no torn file possible.

**Schema drift handling** (FR-010): on read, any of {file missing, deserialization fails, `schema_version` unknown, required field missing, `dictionary` payload doesn't satisfy domain validation} returns `FetchFailureReason.CacheUnreadable` and the file is left in place (not deleted — leaving it lets a developer inspect what went wrong; the next successful fetch overwrites it).

### `Installation` (new, in `Core.FSharp/Dictionary/`)

Identity unit. Per CHK011 resolution, an Installation is one Windows user account on one workstation. Used as the scope key for both the dictionary cache (FR-013) and the credential (FR-011 family). F# record — structural equality is automatic.

```fsharp
type Installation = {
    MachineName: string        // Environment.MachineName at provisioning time
    UserSid: string             // WindowsIdentity.GetCurrent().User.Value
    InstallationId: Guid        // generated at first run; persisted in DPAPI alongside the credential
}
```

Equality: F# records are structurally equal on all fields by default. The CHK011 contract requires equality on `(MachineName, UserSid)` while `InstallationId` is metadata. To express this precisely, the comparison helper used at the credential-store boundary (in `DpapiCredentialStore.GetInstallationAsync`'s mismatch detection) compares only `(MachineName, UserSid)`; the F# `=` operator on the full record stays available for tests that want full equality. The `InstallationId` is identity-bearing only for the server's records — it surfaces on the wire as `descriptor.installGuid` in the bootstrap-exchange JSON body (`contracts/register-api.md`, R-1 follow-up). The on-wire `descriptor.machineId` and `descriptor.osUserId` are SHA-256 hashes of `MachineName` and `UserSid` respectively (resolved 2026-05-07 — see contract); the in-memory `Installation` record stays unhashed because the local equality semantics depend on the raw values.

### `CredentialLifecycleState` (new, in `Core.FSharp/Dictionary/`)

F# discriminated union mirroring FR-011e. A DU is more idiomatic than an enum here because future variants might carry data (e.g. `Expired of expiredAt: DateTimeOffset`) and the DU representation makes that future-safe.

```fsharp
type CredentialLifecycleState =
    | Provisioned    // just unwrapped from installer-bundle (R-1 path) or returned by POST /register (future, contracts/register-api.md)
    | Active         // post-first-successful-API-call validation
    | Rotated        // server-side replaced (with grace window) — runtime-equivalent to Revoked
    | Revoked        // server-side invalidated, immediate
    | Expired        // validity period exceeded — only meaningful if server policy uses time-bounded keys
```

The client only ever observes `Provisioned`, `Active`, or "needs re-provisioning" (collapsing `Rotated`/`Revoked`/`Expired` together — they all surface as `FetchFailureReason.Unauthorized` per FR-011f). The DU exists in `Core.FSharp` so server-side and operational discussions have shared vocabulary, even though the client's branching is binary.

## Interfaces

### `IDictionaryProvider` (`Core.FSharp/Dictionary/IDictionaryProvider.fs`)

```fsharp
type IDictionaryProvider =
    abstract FetchAsync: CancellationToken -> Task<DictionaryFetchResult>
```

Two C# implementations consume this F# interface (CLR interop is transparent): `HttpDictionaryClient` (live API) and `JsonFileDictionaryCache` (disk fallback). The F# `DictionaryService` in `Services.FSharp` composes them. Both implementations must honour cancellation (Constitution I, `CANCELLATION` standard) — the cache implementation cancels mid-deserialization; the HTTP implementation cancels mid-flight.

### `IInstallationCredentialStore` (`Core.FSharp/Dictionary/IInstallationCredentialStore.fs`)

```fsharp
type IInstallationCredentialStore =
    abstract GetApiKeyAsync: CancellationToken -> Task<string voption>          // ValueNone = no credential provisioned yet
    abstract SetApiKeyAsync: apiKey: string * CancellationToken -> Task
    abstract ClearAsync: CancellationToken -> Task                                // re-provisioning hook (FR-011f)
    abstract GetInstallationAsync: CancellationToken -> Task<Installation voption>
```

Single C# production implementation: `DpapiCredentialStore` in `Infrastructure/Dictionary/`. F# manual fake for tests in the test project. `ClearAsync` is the FR-011f re-provisioning hook; on the next launch, an absent key triggers `FetchFailureReason.SetupIncomplete` and the bundle-unwrap (or future bootstrap-exchange) flow re-runs.

**Interop note**: `string voption` and `Installation voption` (F# `ValueOption`) surface to C# as `Microsoft.FSharp.Core.FSharpValueOption<T>`. That's awkward in C# call sites, so the C# `DpapiCredentialStore` implementation will likely shadow these with explicit `?.HasValue`/`?.Value` patterns or use a thin C#-side adapter. Acceptable trade — F# domain types staying expressive matters more than C# infrastructure ergonomics.

## Validation rules (cross-cutting)

| Rule | Source | Where enforced |
|---|---|---|
| Cache `schema_version` must equal 1 | FR-010, R-7 | `JsonFileDictionaryCache.ReadAsync` |
| Cache `fetched_at` must parse as ISO 8601 with offset | FR-010 | `JsonFileDictionaryCache.ReadAsync` |
| `ButtonPanelDictionary.Variables` non-empty after deserialization | US2 AC2 (malformed → fall back) | Existing domain validation in `Core` (verify in /speckit-tasks) |
| API URL must be absolute HTTPS | Implicit security; not in spec FRs | `DictionaryApiOptions` validator (`IValidateOptions<DictionaryApiOptions>`) |
| Installation equality (`MachineName`, `UserSid`) is invariant for the lifetime of `credential.bin` | Installation entity | `DpapiCredentialStore.GetInstallationAsync` re-checks at each call; mismatch → `Clear` and trigger re-provisioning (covers user-account-reassignment edge case from CHK007) |

## Out of scope (data model)

- Cross-Installation cache sharing (each Installation has its own).
- Cache compression / encryption at rest (the dictionary content itself is not sensitive — only the credential is).
- Schema migration tooling (per spec out-of-scope: schema drift → discard cache, not transform).
- Multi-environment cache layout (FR-013 forward-compatibility: subdirectory addable later; v1 has none).
