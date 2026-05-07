# Contract: `POST /register` (future, dependency-tracked)

**Status**: **NOT REQUIRED FOR v1.0** of `feat/001-dictionary-from-api`. **Aligned** with server-side spec on 2026-05-07.
**Direction**: this app **consumes** this endpoint.
**Server-side authority**: [`stem-dictionaries-manager:specs/001-bootstrap-registration/spec.md`](https://github.com/luca-veronelli-stem/stem-dictionaries-manager/blob/main/specs/001-bootstrap-registration/spec.md) (currently on the `001-bootstrap-registration` branch — will land on `main` when that PR merges). Tracking issue: [`stem-dictionaries-manager#1`](https://github.com/luca-veronelli-stem/stem-dictionaries-manager/issues/1).

This contract documents the bootstrap-exchange path described in spec.md FR-011 family. Per R-1 (`research.md`), v1.0 of this app uses the per-supplier installer + DPAPI mechanism; the bootstrap-exchange path is the planned migration once the server endpoint ships. The shape below matches the server-side decisions locked in `stem-dictionaries-manager#1`'s `/speckit-clarify` of 2026-05-07.

## Endpoint

```
POST /register
Host:           <DictionaryApiOptions.BaseUrl>
Content-Type:   application/json
```

**Path is unversioned.** `stem-dictionaries-manager#1` prescribes `/register` (no `/v1/` prefix). The dictionary endpoint version mismatch (`/v1/dictionary` consumer-side vs server's `/api/dictionaries`) is a separate conversation tracked elsewhere — out of scope for this contract.

**No `Authorization` header.** The bootstrap token is sent in the JSON body as `bootstrapToken`, not as a `Bearer` credential. This avoids the Bearer-vs-API-key ambiguity at the auth-middleware layer and keeps the bootstrap exchange visually distinct from steady-state API calls.

### Request body

```json
{
  "bootstrapToken": "stbt_<43-char-base64url>",
  "descriptor": {
    "installGuid":  "5b3f1a4e-...-9c2d",
    "machineId":    "sha256:e1c4...",
    "osUserId":     "sha256:8a92...",
    "clientApp":    "ButtonPanelTester",
    "appVersion":   "1.0.0"
  }
}
```

Server-wide JSON naming policy is **camelCase** (`JsonNamingPolicy.CamelCase` in `stem-dictionaries-manager`'s `Program.cs`). All consumer-side serialization must match.

Field rules:
- `bootstrapToken` (string, required) — single-use, time-bounded, supplier-scoped. Format: `stbt_<43-char-base64url>` (256 bits of entropy + type prefix). The `stbt_` prefix is operational only (grep, log triage) — it carries no security claim.
- `descriptor.installGuid` (UUID v4, required) — generated client-side at first run, persisted in DPAPI alongside the credential. Used by the server to identify this Installation in admin tooling and audit logs. Survives reinstalls only if DPAPI storage survives.
- `descriptor.machineId` (string, required) — opaque-to-server stable identifier. Consumer chooses to send `sha256:<hex>` of the raw machine name (deterministic, unsalted; the `sha256:` namespace prefix preserves "same install" correlation across reinstalls without exposing the human-readable hostname). See "Hashing decision" below.
- `descriptor.osUserId` (string, required) — opaque-to-server stable identifier. Same hashing treatment as `machineId`.
- `descriptor.clientApp` (string, required) — the bootstrap token's minted scope, by convention the `ApiKeys` config-key form. For this consumer the constant is **`"ButtonPanelTester"`** (not the repo name `stem-button-panel-tester`). Matched server-side as a free-text byte-comparison against the bootstrap token's scope.
- `descriptor.appVersion` (semver string, optional) — for ops correlation when version-specific issues surface. Server persists it on `Installation` and `RegistrationEvent`.

#### Hashing decision (resolved 2026-05-07)

`button-panel-tester` ships to external suppliers, so the server-side admin (STEM) and the OS user (supplier employee) are different organizations. Privacy/data-minimization wins; cross-org incident response already routes through the supplier's IT contact, so ops triage does not actually depend on seeing raw hostnames server-side.

- **Hash**: SHA-256 of UTF-8 bytes, deterministic, **unsalted**.
- **Wire format**: `sha256:<lowercase-hex>` (the `sha256:` namespace prefix is fixed, so future hash-algorithm changes remain detectable).
- **Sources**:
  - `machineId` = `sha256:` + SHA-256 of `Environment.MachineName`.
  - `osUserId`  = `sha256:` + SHA-256 of the user SID string (`SecurityIdentifier.Value`, format `S-1-5-21-…`).
- **Stability**: unsalted is intentional — same install on the same machine/user produces the same hash, so admin can correlate "this is the same Installation" without seeing the underlying values.

## Success response — `200 OK`

```json
{
  "apiCredential": "stak_<43-char-base64url>",
  "issuedAt":      "2026-05-06T11:23:45.000Z"
}
```

Field rules:
- `apiCredential` (string, required) — the long-lived per-Installation credential. Format: `stak_<43-char-base64url>`. Client immediately encrypts via DPAPI and writes to `credential.bin`; never persists the cleartext.
- `issuedAt` (ISO 8601 UTC, required) — for the client's audit/log records.
- **No `expiresAt` field.** API credentials are long-lived until admin-revoked (FR-006 server-side). The consumer must not rely on a timestamp here. Existing consumer code that handles `expires_at: null` is fine; it just never observes a non-null value.

**Server-side guarantee — plaintext returned exactly once.** Per FR-014/SC-007 of `stem-dictionaries-manager#1`, the `apiCredential` plaintext is generated, returned in this response, and immediately discarded server-side (only a hash is persisted). It is **not** re-issuable. See "Failure recovery" below.

## Error responses — unified failure

| Status | Body | Client interpretation |
|---|---|---|
| `401 Unauthorized` | `{"error": "registration failed"}` | Any failure mode: token unknown / used / expired, supplier revoked, descriptor already registered, malformed request. Maps to `FetchFailureReason.SetupIncomplete`. |
| `5xx` | any | Server error. Client retries on next launch only (FR-011d "no retry storm"). |
| Network failure | (no HTTP) | Same as `5xx`. |

**Single failure body is intentional.** Distinguishable codes (`401 token_invalid`, `403 supplier_revoked`, `409 already_registered`) were considered and rejected by `stem-dictionaries-manager#1`'s clarification: they form a token-status oracle for an attacker harvesting tokens from disassembled installers. The consumer-side error mapping must collapse to a single `FetchFailureReason.SetupIncomplete` on any non-200 response — do not try to distinguish causes from the body.

## Failure recovery — operator-side, not idempotent

`POST /register` is **not idempotent**. Re-issuing plaintext on a duplicate descriptor is the anti-pattern this whole feature exists to fix (it requires storing plaintext server-side). Concretely, an earlier draft of this contract proposed `409 already_registered → return existing key`; that path is rejected.

If `/register` partially fails — e.g., the consumer's `credential.bin` write fails after the server-side Installation row is created — the recovery path is **operator-side**:

1. Consumer reports the setup failure via the FR-011d UI/log path. No automatic retry within the launch.
2. Admin (server-side) revokes the half-created Installation and mints a fresh bootstrap token.
3. Fresh bootstrap token is delivered to the supplier through the same out-of-band channel as the first one.
4. Consumer re-runs the bootstrap exchange on next launch with the new token.

There is no "retry with same `installGuid`" path. A fresh bootstrap exchange yields a fresh `installGuid` (generated client-side at the start of the new attempt) and a fresh `apiCredential`.

## Bootstrap-token lifecycle (server-side, informational)

- Bootstrap tokens are minted by an admin against a specific supplier scope (e.g. via CLI: `stemdict admin mint-bootstrap --scope ButtonPanelTester --supplier acme-instruments`). The exact CLI shape is out of scope of this contract.
- Bootstrap token is **single-use** — `POST /register` succeeds at most once per token; subsequent calls return `401 registration failed` per the unified-failure rule above.
- The minted bootstrap token is delivered to the build pipeline via secure channel (depends on STEM's CI/CD setup) and embedded into the per-supplier installer's transport-encrypted bundle.
- Token format: `stbt_<43-char-base64url>` (256 bits of entropy). API credentials use `stak_` with the same shape. Prefixes are operational only.

## Migration path from v1.0 (installer-bundle) to bootstrap-exchange

When `stem-dictionaries-manager#1` ships:

1. Client gains a new `BootstrapExchangeCredentialProvisioner` in `Infrastructure/Dictionary/`. Hardcoded `clientApp = "ButtonPanelTester"`. Camel-case JSON serialization. JSON-body bootstrap token (no `Authorization` header).
2. On first run with no credential present, the new code path is preferred over the installer-bundle unwrap.
3. Existing installations from the installer-bundle path are unaffected — their `credential.bin` continues to work; they only switch to the new flow if `IInstallationCredentialStore.ClearAsync` fires (FR-011f auth-failure → re-provisioning).
4. After ~6 months in production, the installer-bundle code path can be removed in a major bump.

No behavioural change visible to the supplier across the migration — they don't know or care which provisioning mechanism produced their `credential.bin`.

## Test coverage (when this lands)

`/speckit-tasks` for the bootstrap migration will need:
- Happy path: valid bootstrap token → `200 { apiCredential, issuedAt }` → key persisted via DPAPI → first `GET /v1/dictionary` succeeds with the new key.
- Any non-200 response → `FetchFailureReason.SetupIncomplete` via the unified-failure mapping. WireMock fixtures should test at least `401 { error: "registration failed" }` and a `5xx` to exercise the same code path.
- **No `409 already_registered` test** — that behaviour does not exist server-side. The earlier draft test for it must not be ported.
- camelCase serialization round-trip — the request body and the response body are asserted field-by-field against the server-side JSON shape.
