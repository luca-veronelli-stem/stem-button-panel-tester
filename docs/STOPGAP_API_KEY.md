# Dictionary credential — stopgap (plaintext API key in config)

**Status:** active
**Introduced:** 2026-05-08, branch `feat/dictionary-api-key-config-stopgap`
**Re-secure tracking issue:** [#50](https://github.com/luca-veronelli-stem/stem-button-panel-tester/issues/50)

## What changed

The `stem-button-panel-tester` build that ships today reads the
`stem-dictionaries-manager` API key directly from configuration
(`Dictionary:ApiKey`, override via env var `Dictionary__ApiKey`) and sends it on
the wire as `X-Api-Key` — matching the pattern used by `stem-device-manager`
against the same shared `stem-dictionaries-manager` deployment.

The DPAPI-backed credential store and the first-run installer-bundle unwrap
step (`DictionaryCredentialBootstrap.EnsureAsync`) are bypassed:

- `IInstallationCredentialStore` is bound to `PlaceholderInstallationCredentialStore`
  unconditionally (was: `DpapiCredentialStore` on Windows + bootstrap step;
  Placeholder only on non-Windows).
- `HttpDictionaryClient` sends `X-Api-Key: <key>` (was: `Authorization: Bearer <key>`).
- `appsettings.Production.json` is gitignored and carries the supplier key.

`DpapiCredentialStore.cs`, the `IInstallationCredentialStore` F# contract, and
the DPAPI tests are intentionally retained on disk — reinstating the secure
path is a recompose, not a re-implement.

## Why

Same-day delivery of an API-backed dictionary build was needed before the
secure provisioning path (per-supplier installer bundle with transport-encrypted
key unwrapping into DPAPI on first run, per `specs/001-dictionary-from-api/`
research item R-1) was ready. Copying the device-manager pattern was the
shortest path that the team already operates in production.

## Spec violations consciously waived

This stopgap violates parts of the `001-dictionary-from-api` spec; the
violations are deliberate and bounded to this build:

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

## What it would take to re-secure

The DPAPI machinery is preserved, so the re-secure PR is small:

1. Restore the `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` branch in
   `src/GUI.WinForms/Composition/DictionaryComposition.cs` and register
   `DpapiCredentialStore` + `DictionaryCredentialBootstrap`.
2. Restore the `bootstrap.EnsureAsync(...)` call in
   `src/GUI.WinForms/Program.cs` after `BuildServiceProvider`.
3. Switch `HttpDictionaryClient` back to `Authorization: Bearer <key>` if the
   server-side accepts both, or keep `X-Api-Key` if the server settles on it.
4. Implement R-1 (per-supplier installer bundle with transport-encrypted key)
   so FR-011b's "no manual configuration step" is honoured for new
   installations. Until then, the bootstrap step keeps reading
   `Dictionary:ApiKey` from config, which is at least one rotation cycle
   safer than the current state but still violates FR-011a in installer
   artefacts.

The follow-up issue tracks (4) — the harder of the four — and the smaller
restore steps will land alongside it.
