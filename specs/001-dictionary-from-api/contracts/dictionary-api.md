# Contract: `GET /v1/dictionary`

**Direction**: this app **consumes** `stem-dictionaries-manager`'s API.
**Stability**: required for v1.0 of `feat/001-dictionary-from-api`.
**Counterparty**: `stem-dictionaries-manager` repository — must agree to the shape below or update this contract before the feature ships.

This is the contract the `HttpDictionaryClient` codes against. Tests in `tests/Tests/Infrastructure/Dictionary/HttpDictionaryClientTests.cs` use `WireMock.Net` to stub responses matching this contract; any drift between this document and the real server response will surface as `FetchFailureReason.MalformedPayload` at runtime.

## Endpoint

```
GET /v1/dictionary
Host:          <DictionaryApiOptions.BaseUrl>
Authorization: Bearer <api-key>
Accept:        application/json
```

The host and path major-version segment (`/v1/`) come from `DictionaryApiOptions`. The API key is fetched from `IInstallationCredentialStore.GetApiKeyAsync` and is the per-Installation credential per FR-011 + FR-011a-f.

## Success response — `200 OK`

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{
  "schema_version": 1,
  "generated_at": "2026-05-06T11:23:45.000Z",
  "panel_types": [
    {
      "id": "BP-12-A",
      "display_name": "Button Panel 12 (variant A)",
      "variables": [
        {
          "name": "voltage_input",
          "type": "uint16",
          "address": 4097,
          "scaling": 0.01,
          "unit": "V"
        }
      ]
    }
  ]
}
```

Field rules:
- `schema_version` (int, required) — must equal `1`. Any other value → client treats as `MalformedPayload`.
- `generated_at` (ISO 8601 UTC, required) — server-side authoritative timestamp; the client stores this in the cache envelope as `fetched_at` (clarification: prefer the server timestamp over local clock for cache consistency across users).
- `panel_types` (array, required, non-empty) — empty array is treated as malformed (US2 AC2) since a real dictionary always has at least one panel type.
- Each `panel_types[i]` has the existing `ButtonPanelDictionary` shape — exact field set defined by the existing `Core` domain types.

## Error responses

| Status | Body | Client interpretation | Maps to |
|---|---|---|---|
| `401 Unauthorized` | any (typically `{"error": "..."}`) | Credential rotated, revoked, or never valid. Engage FR-011f. | `FetchFailureReason.Unauthorized` |
| `403 Forbidden` | any | Same as `401` for client purposes (auth-distinct from network). | `FetchFailureReason.Unauthorized` |
| `404 Not Found` | any | Either `/v1/` is gone (server cut to `/v2/`) or the path is wrong. Maps to malformed for fallback purposes; client logs distinctively. | `FetchFailureReason.MalformedPayload` (with detail "endpoint not found") |
| `5xx` | any | Server error. | `FetchFailureReason.ServerError` |
| Network/DNS/TLS failure | (no HTTP) | Connection failure. | `FetchFailureReason.NetworkUnreachable` |
| Read deadline exceeded (5s, FR-012) | (partial or none) | Timeout. | `FetchFailureReason.Timeout` |
| `200 OK` with non-JSON, missing required fields, or `schema_version != 1` | any | Schema drift or content bug on server. Cache unaffected; existing cache (if any) is preserved per US2 AC2. | `FetchFailureReason.MalformedPayload` |

The client does **not** distinguish `4xx`-other from `MalformedPayload` for runtime control flow — all of "we got something, but couldn't parse it as a usable dictionary" share one fallback path. Logging carries the actual status code per FR-014.

## Versioning

Path-based per R-9: `/v1/`. A future major bump (`/v2/`) is an explicit cutover handled by shipping a new client version with `DictionaryApiOptions.MajorVersion = "v2"`. Old `/v1/` clients hitting a `/v2/`-only server get `404` and fall back to cache while logging "endpoint not found" — manageable degradation, not silent corruption.

## Test coverage required (`/speckit-tasks` will produce these)

- `HttpDictionaryClient` test for each `FetchFailureReason` from the table above.
- Integration test: `WireMock.Net` returns the success body above; client deserializes successfully; cache writer round-trips it; cache reader produces an equivalent `ButtonPanelDictionary`.
- Negative integration: `WireMock.Net` returns `200` with truncated JSON → `FetchFailureReason.MalformedPayload`, existing cache untouched.

## Coordination with `stem-dictionaries-manager`

Cross-link: this contract and `register-api.md` are both speculative until reviewed by the `stem-dictionaries-manager` team. The `panel_types` shape above must be confirmed against the existing server-side dictionary schema (which today produces `.xlsx`, not JSON) — there may be field-name or type mismatches that need a translation layer. Track in `stem-dictionaries-manager` issues alongside `#1`.
