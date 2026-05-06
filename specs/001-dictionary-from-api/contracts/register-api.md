# Contract: `POST /v1/register` (future, dependency-tracked)

**Status**: **NOT REQUIRED FOR v1.0** of `feat/001-dictionary-from-api`.
**Direction**: this app would **consume** this endpoint when/if it ships.
**Dependency**: [`stem-dictionaries-manager#1`](https://github.com/luca-veronelli-stem/stem-dictionaries-manager/issues/1).

This contract documents the bootstrap-exchange path described in spec.md FR-011 family. Per R-1 (`research.md`), v1.0 of this app uses the per-supplier installer + DPAPI mechanism instead, because `/register` does not yet exist server-side. This document captures what we'd want the endpoint to look like so that:

1. The follow-up migration to bootstrap-exchange is a small, contract-driven change.
2. The `stem-dictionaries-manager` team has a concrete starting shape to react to in `#1`.

## Endpoint (proposed)

```
POST /v1/register
Host:           <DictionaryApiOptions.BaseUrl>
Content-Type:   application/json
Authorization:  Bearer <bootstrap-token>      ← single-use, time-bounded, supplier-scoped
```

**Note on auth header**: the bootstrap token is presented in the same `Authorization: Bearer` slot that the eventual API key will use. The server distinguishes them by token format (e.g., a prefix `bs_` for bootstrap, `key_` for API keys) or by lookup table — implementation detail of `stem-dictionaries-manager`.

### Request body

```json
{
  "installation_descriptor": {
    "installation_id": "5b3f1a4e-...-9c2d",
    "machine_name_hash": "sha256:e1c4...",
    "user_sid_hash":     "sha256:8a92...",
    "app":               "stem-button-panel-tester",
    "app_version":       "1.0.0"
  }
}
```

Field rules:
- `installation_id` (UUID v4, required) — generated client-side at first run, persisted in DPAPI alongside the credential. Used by the server to identify this Installation in admin tooling and audit logs.
- `machine_name_hash`, `user_sid_hash` (SHA-256 hex, required) — hashed because the raw values aren't useful to the server but the hashes are stable identifiers (admin can correlate "this is the same machine" without seeing the hostname). Open question for `stem-dictionaries-manager#1`: whether these hashes are mandatory or optional.
- `app` (string, required) — repository name; lets the server distinguish requests from `stem-button-panel-tester` vs `stem-device-manager` once both apps adopt this flow.
- `app_version` (semver string, required) — for ops correlation when version-specific issues surface.

## Success response — `200 OK`

```json
{
  "api_key": "key_eyJ...",
  "expires_at": null,
  "issued_at": "2026-05-06T11:23:45.000Z"
}
```

Field rules:
- `api_key` (string, required) — the long-lived per-Installation credential. Client immediately encrypts via DPAPI and writes to `credential.bin`; never persists the cleartext.
- `expires_at` (ISO 8601 UTC | null, required) — `null` indicates no server-side expiry policy (key is rotated only on revocation or admin action). A timestamp would mean the client should expect re-provisioning before that time. **Open question for `#1`**: whether keys are time-bounded by default.
- `issued_at` (ISO 8601 UTC, required) — for the client's audit/log records.

## Error responses

| Status | Body | Client interpretation |
|---|---|---|
| `401 Unauthorized` | `{"error": "bootstrap_token_invalid"}` | Token unknown, used, or expired. Maps to `FetchFailureReason.SetupIncomplete` with the bootstrap-failure path of FR-011d (distinct user-facing error vs general 401). |
| `403 Forbidden` | `{"error": "supplier_revoked"}` | Token's supplier has been disabled. Same client handling as `401` but distinct log message. |
| `409 Conflict` | `{"error": "already_registered"}` | The `installation_id` is already known to the server. Server may return the existing `api_key` (idempotent) or refuse — `#1` decision. Idempotent is preferred so re-running the bootstrap on a partially-failed first run is safe. |
| `5xx` | any | Server error. Client retries on next launch only (FR-011d "no retry storm"). |
| Network failure | (no HTTP) | Same as `5xx`. |

## Bootstrap-token lifecycle (server-side, informational)

Per the proposal in `stem-dictionaries-manager#1`:

- Bootstrap tokens are minted by an admin against a specific supplier (e.g. via CLI: `stemdict admin mint-bootstrap --supplier acme-instruments`).
- Default validity: **30 days** from issue (open in `#1`; subject to change).
- Bootstrap token is **single-use** — `POST /register` succeeds at most once per token; subsequent calls return `401 bootstrap_token_invalid`.
- The minted bootstrap token is delivered to the build pipeline via secure channel (out of scope of this contract — depends on STEM's CI/CD setup) and embedded into the per-supplier installer's transport-encrypted bundle.

## Migration path from v1.0 (installer-bundle) to bootstrap-exchange

When `stem-dictionaries-manager#1` ships:

1. Client gains a new `BootstrapExchangeCredentialProvisioner` in `Infrastructure/Dictionary/`.
2. On first run with no credential present, the new code path is preferred over the installer-bundle unwrap.
3. Existing installations from the installer-bundle path are unaffected — their `credential.bin` continues to work; they only switch to the new flow if `IInstallationCredentialStore.ClearAsync` fires (FR-011f auth-failure → re-provisioning).
4. After ~6 months in production, the installer-bundle code path can be removed in a major bump.

No behavioural change visible to the supplier across the migration — they don't know or care which provisioning mechanism produced their `credential.bin`.

## Test coverage (when this lands)

`/speckit-tasks` for the bootstrap migration will need:
- Happy path: valid bootstrap token → `200` → key persisted via DPAPI → first `GET /v1/dictionary` succeeds with the new key.
- `409 already_registered` returns the existing key (idempotent contract, if confirmed in `#1`).
- `401 bootstrap_token_invalid` triggers FR-011d setup-failure path.
- `5xx` is treated as fail-fast for this launch; retry on next launch.
