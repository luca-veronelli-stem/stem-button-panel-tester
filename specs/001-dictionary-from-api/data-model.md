# Data Model: Dictionary from stem-dictionaries-manager API

**Phase**: 1 — Design & Contracts
**Date**: 2026-05-06
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

This document specifies the entities, fields, relationships, validation rules, and state transitions introduced by this feature. Implementation lives under `Core/Dictionary/` (interfaces + entities) and `Infrastructure/Dictionary/` (on-disk envelope).

## Entities

### `ButtonPanelDictionary` (already exists; reused)

Domain type representing the loaded dictionary. **Shape unchanged by this feature** — the source changes (API or cache instead of `.xlsx`), the contract does not. Existing consumers in `Communication`, `Services`, and `GUI.WinForms` see the same type.

Implementation note: confirm during `/speckit-tasks` that the existing `ButtonPanelDictionary` in `Core` doesn't depend on Excel-specific types (it shouldn't, but verify — issue #21 explicitly tracks purging `System.Drawing` and WinForms types from Core contracts).

### `DictionaryFetchResult` (new, in `Core/Dictionary/`)

Sealed class hierarchy (or record polymorphism) representing the outcome of a single fetch attempt — what `HttpDictionaryClient` and `JsonFileDictionaryCache` both return. Discriminated union shape (C# 13 sealed class hierarchy):

| Variant | Fields | Meaning |
|---|---|---|
| `Success` | `ButtonPanelDictionary Dictionary`, `DateTimeOffset FetchedAt` | A valid dictionary was obtained. `FetchedAt` is the wall-clock time when the response completed (for live) or when the cache was originally written (for cache). |
| `Failed` | `FetchFailureReason Reason`, `string? Detail` | The fetch did not yield a dictionary. `Reason` is the discriminator. |

`FetchFailureReason` enum:

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

### `DictionarySource` (new, in `Core/Dictionary/`)

Sealed class hierarchy representing the origin of the **active** in-memory dictionary. Drives FR-005's UI indicator.

| Variant | Fields | Meaning |
|---|---|---|
| `Live` | `DateTimeOffset FetchedAt` | The active dictionary came from a successful API fetch in this session. |
| `Cached` | `DateTimeOffset FetchedAt`, `FetchFailureReason FallbackReason` | The active dictionary came from the cache because the live fetch failed. `FallbackReason` carries the why so FR-014 logging and FR-005 UI can distinguish "API down" from "credential issue" (CHK017 resolution). |

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

### `Installation` (new, in `Core/Dictionary/`)

Identity unit. Per CHK011 resolution, an Installation is one Windows user account on one workstation. Used as the scope key for both the dictionary cache (FR-013) and the credential (FR-011 family).

Fields:
- `string MachineName` — `Environment.MachineName` at provisioning time.
- `string UserSid` — current Windows user's SID (`WindowsIdentity.GetCurrent().User.Value`).
- `Guid InstallationId` — generated at first run; persisted in DPAPI alongside the credential. The `installation_descriptor` sent to `stem-dictionaries-manager` if/when bootstrap-exchange is adopted (R-1 follow-up).

Equality: structural on (`MachineName`, `UserSid`). `InstallationId` is identity-bearing only for the server's records; locally it's just metadata.

### `CredentialLifecycleState` (new, in `Core/Dictionary/`)

Enum mirroring FR-011e:

```csharp
public enum CredentialLifecycleState
{
    Provisioned,  // just unwrapped from installer-bundle (R-1 path) or returned by /register (future)
    Active,       // post-first-successful-API-call validation
    Rotated,      // server-side replaced (with grace window) — runtime-equivalent to Revoked
    Revoked,      // server-side invalidated, immediate
    Expired       // validity period exceeded — only meaningful if server policy uses time-bounded keys
}
```

The client only ever observes `Provisioned`, `Active`, or "needs re-provisioning" (collapsing `Rotated`/`Revoked`/`Expired` together — they all surface as `FetchFailureReason.Unauthorized` per FR-011f). The enum exists in `Core` so server-side and operational discussions have shared vocabulary, even though the client's branching is binary.

## Interfaces

### `IDictionaryProvider` (`Core/Dictionary/IDictionaryProvider.cs`)

```csharp
public interface IDictionaryProvider
{
    Task<DictionaryFetchResult> FetchAsync(CancellationToken ct);
}
```

Two implementations: `HttpDictionaryClient` (live API) and `JsonFileDictionaryCache` (disk fallback). `DictionaryService` composes them. Both must be cancellable (Constitution I, `CANCELLATION` standard) — the cache implementation cancels mid-deserialization; the HTTP implementation cancels mid-flight.

### `IInstallationCredentialStore` (`Core/Dictionary/IInstallationCredentialStore.cs`)

```csharp
public interface IInstallationCredentialStore
{
    Task<string?> GetApiKeyAsync(CancellationToken ct);   // null = no credential provisioned yet
    Task SetApiKeyAsync(string apiKey, CancellationToken ct);
    Task ClearAsync(CancellationToken ct);                // for re-provisioning after auth failure
    Task<Installation?> GetInstallationAsync(CancellationToken ct);
}
```

Single production implementation: `DpapiCredentialStore` in `Infrastructure/Dictionary/`. Manual fake for tests. `ClearAsync` is the FR-011f re-provisioning hook; on the next launch, the absence of a key triggers `FetchFailureReason.SetupIncomplete` and the bundle-unwrap (or future bootstrap-exchange) flow re-runs.

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
