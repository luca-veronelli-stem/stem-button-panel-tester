# Dictionary credential + endpoint — stopgap

**Status:** active
**Introduced:** 2026-05-08, branch `feat/dictionary-api-key-config-stopgap`
**Re-secure tracking issue:** [#50](https://github.com/luca-veronelli-stem/stem-button-panel-tester/issues/50)

## What changed

The `stem-button-panel-tester` build that ships today differs from the
[`001-dictionary-from-api`](../specs/001-dictionary-from-api/) spec on three
axes simultaneously, all to match the actual `stem-dictionaries-manager`
deployment used by `stem-device-manager`:

### 1. Credential storage — plaintext config, no DPAPI

The DPAPI-backed credential store and the first-run installer-bundle unwrap
step (`DictionaryCredentialBootstrap.EnsureAsync`) are bypassed:

- `IInstallationCredentialStore` is bound to `PlaceholderInstallationCredentialStore`
  unconditionally (was: `DpapiCredentialStore` on Windows + bootstrap step;
  Placeholder only on non-Windows).
- The API key is read directly from `Dictionary:ApiKey` (override via env var
  `Dictionary__ApiKey`; supplier-specific value lives in gitignored
  `appsettings.Production.json`).

### 2. Wire-level auth header — `X-Api-Key`, not Bearer

- `HttpDictionaryClient` sends `X-Api-Key: <key>` (was: `Authorization: Bearer <key>`).

### 3. Endpoint + payload shape — `/api/dictionaries/{id}/resolved`

The speced `GET /v1/dictionary` endpoint is **not implemented server-side**;
the actual `stem-dictionaries-manager` surface is the device-manager-style
multi-endpoint set (`/api/devices`, `/api/dictionaries`,
`/api/dictionaries/{id}/resolved`, `/api/boards/{id}/definition`, …). The
client now calls `GET /api/dictionaries/{DictionaryId}/resolved` (default
`DictionaryId = 2` — "Pulsantiere") and assembles a single-`PanelType`
`ButtonPanelDictionary` from the response.

Per-field mapping from the server's `DictionaryResolvedDto` to the client's
domain `Variable`:

| client field | source |
|---|---|
| `Address` | `(addressHigh << 8) \| addressLow` |
| `Type`    | `dataType` (passthrough — `"UInt16"`, `"Bool"`, `"Bitmapped[4]"`, `"UInt8"`, …) |
| `Name`    | `name` |
| `Unit`    | `unit ?? ""` (server may omit) |
| `Scaling` | **hardcoded `1.0`** — the server's wire shape does not carry a scaling field. ⚠️ |

`PanelType.Id` is the dictionary id stringified; `PanelType.DisplayName` is the
dictionary name (e.g. `"Pulsantiere"`). The `panel_types[]` tree shape from
[`contracts/dictionary-api.md`](../specs/001-dictionary-from-api/contracts/dictionary-api.md)
is the design intent and is tracked for re-instatement.

### Retained on disk (not deleted)

`DpapiCredentialStore.cs`, the `IInstallationCredentialStore` F# contract, the
DPAPI tests, and the original `DictionaryResponseDto` shape concept are
intentionally retained — reinstating the secure path is a recompose, not a
re-implement.

## Why

Same-day delivery of an API-backed dictionary build was needed before the
secure provisioning path (per-supplier installer bundle with transport-encrypted
key unwrapping into DPAPI on first run, per `specs/001-dictionary-from-api/`
research item R-1) was ready. Copying the device-manager pattern was the
shortest path that the team already operates in production.

## Spec violations consciously waived

This stopgap violates parts of the `001-dictionary-from-api` spec; the
violations are deliberate and bounded to this build:

### Credential / auth violations

- **FR-011a — "No plaintext credential MUST appear on disk during normal
  operation, including in installer artefacts left on disk after install."**
  The key lives plaintext in `appsettings.Production.json` inside the deployed
  artefact. ⚠️
- **FR-011b — "Initial credential provisioning MUST NOT require any manual
  configuration step by the supplier."** Each new supplier deployment requires
  the build pipeline (or the engineer cutting the release) to drop in
  `appsettings.Production.json` before packaging. ⚠️
- **FR-011c — "Per-Installation API credential revocation MUST be possible
  without redeploying the app... One supplier's compromised credential MUST NOT
  blast-radius to other installations."** The same key is shared across every
  installation built from the same artefact. A compromise blast-radiuses to
  every supplier on that build until a new key + rebuild + redeploy.
- **FR-011d / FR-011e / FR-011f** continue to behave correctly at the wire
  level (401 → cache fallback, no retry, distinct log level), but the
  re-provisioning recovery path is "rebuild and ship a new installer", not
  "re-run the bootstrap on next launch".
- **Constitution Principle I — `CONFIGURATION` standard's "secret never in
  config".** Explicitly violated. The constitution's Compliance gate is failed
  for this branch; this document is the recorded justification.

### Wire-shape violations

- **The contract at [`contracts/dictionary-api.md`](../specs/001-dictionary-from-api/contracts/dictionary-api.md)
  is not what the runtime sends.** The endpoint, the request method/headers,
  and the response shape all differ. The contract document carries a
  stopgap callout pointing at this file.
- **`Variable.Scaling` is invented client-side, not derived from the server.**
  Hardcoded to `1.0` for every variable. Variables that should be displayed
  as scaled (e.g. raw `0.01` per LSB) will show the raw integer value in the
  GUI. This is acceptable for the `"Pulsantiere"` dictionary specifically
  because its variables are mostly LED/buzzer command bitmaps, status enums,
  and `Bool` flags — none of which need scaling. Adding new variables that
  need scaling is a known footgun until the server exposes the field. ⚠️
- **Single PanelType.** The client domain models multiple `panel_types`; the
  stopgap collapses one server-side dictionary to one client-side PanelType.
  Multi-panel-type semantics are unreachable from this wire shape until the
  server adds a richer endpoint or the client iterates over multiple
  dictionary ids.

## Operational notes

- **The shared API key.** Same key as `stem-device-manager` against the same
  `stem-dictionaries-manager` server. Rotation is coordinated with the
  device-manager team — rotating breaks both products until both rebuild.
- **CI.** `appsettings.Production.json` is gitignored, so CI builds are
  unauthenticated against the live API. Tests use `WireMock.Net` and are
  unaffected.
- **Local dev.** Set `Dictionary__ApiKey` in your environment, or drop a local
  `appsettings.Production.json` next to the GUI executable. Either way the key
  must not be committed.
- **Local dev against a self-hosted `stem-dictionaries-manager`.** Override
  `Dictionary:BaseUrl` to e.g. `http://localhost:5062/` (the validator allows
  `http` for any host since the API key is already plaintext on disk in this
  build). Default `DictionaryId` is `2` ("Pulsantiere"); override via
  `Dictionary:DictionaryId` if you target a different dictionary id locally.

## What it would take to re-secure / re-align

The DPAPI machinery is preserved, so the credential-restore PR is small. The
wire-shape restore is bigger because it depends on a server-side change.

### Credential / auth (small, client-side)

1. Restore the `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` branch in
   `src/GUI.WinForms/Composition/DictionaryComposition.cs` and register
   `DpapiCredentialStore` + `DictionaryCredentialBootstrap`.
2. Restore the `bootstrap.EnsureAsync(...)` call in
   `src/GUI.WinForms/Program.cs` after `BuildServiceProvider`.
3. Switch `HttpDictionaryClient` back to `Authorization: Bearer <key>` if the
   server-side accepts both, or keep `X-Api-Key` if the server settles on it.
4. Tighten `DictionaryApiOptionsValidator` to reject `http` BaseUrls again.

### Wire shape (larger, cross-repo)

5. Decide the server-side direction:
   - **Option A** — add a `GET /v1/dictionary` (or
     `GET /api/dictionaries/{id}/panel-types`) endpoint to
     `stem-dictionaries-manager` that returns the speced
     `panel_types[]` shape, including a `scaling` field per variable.
   - **Option B** — keep the existing endpoint set; rewrite the spec to match
     the device-manager-style multi-endpoint model. The client iterates over
     a configurable set of dictionary ids and produces one `PanelType` per
     dictionary. Add `scaling` to the server's `VariableDto`.
6. Restore the `JsonRequired` invariants on the speced fields and the
   `schema_version != 1` and "empty `panel_types`" malformed-payload checks.
7. Drop the `Variable.Scaling = 1.0` fallback in
   `src/Infrastructure/Dictionary/Dtos/DictionaryResponseDto.cs` — surface
   the real value from the wire.

### Provisioning (R-1 — independent of this stopgap)

8. Implement R-1 (per-supplier installer bundle with transport-encrypted key)
   so FR-011b's "no manual configuration step" is honoured for new
   installations. Until then, the bootstrap step keeps reading
   `Dictionary:ApiKey` from config, which is at least one rotation cycle
   safer than the current state but still violates FR-011a in installer
   artefacts.

The follow-up issue tracks the cross-repo work — the smaller credential
restore (1–4) and the wire-shape decision (5–7) will land alongside R-1 (8).
