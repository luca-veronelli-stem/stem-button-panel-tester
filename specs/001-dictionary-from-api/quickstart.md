# Quickstart: dictionary-from-api

**Phase**: 1 — Design & Contracts
**Date**: 2026-05-06
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Data model**: [data-model.md](./data-model.md)

How to verify this feature works locally, by hand, against a real or fake `stem-dictionaries-manager`. Three scenarios — each maps to a P1 / P2 user story in the spec. Treat this as the manual smoke test before you merge.

## Prerequisites

- Windows 10 or 11 (DPAPI requires it).
- .NET 10 SDK installed.
- Network access to a real `stem-dictionaries-manager` instance, **or** the included `WireMock.Net` fake harness running locally.
- A valid bootstrap-bundle file from the build pipeline at `eng/secrets/<supplier>.bundle.bin`, **or** for development: a literal API key seeded directly via `dotnet run -- --seed-credential <key>` (development-only flag, gated behind `#if DEBUG`).

## Scenario 1 — Live dictionary from a healthy API (US1)

**Validates**: FR-001, FR-002, FR-004, FR-005, SC-004 (≤5s healthy), SC-001 (server change visible on next launch).

```powershell
# Start from a clean slate — no cache, no credential, fresh DPAPI store
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Stem.ButtonPanel.Tester" -ErrorAction SilentlyContinue

# Seed a credential (development path; production gets the bundle)
dotnet run --project src/GUI.WinForms -- --seed-credential "key_dev_<your-test-key>"

# Launch
dotnet run --project src/GUI.WinForms
```

**Expected**:
1. App opens, the dictionary state indicator (FR-005) shows **"Live · just now"** within ≤5s of launch.
2. `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\dictionary.json` now exists; `schema_version: 1`, `fetched_at` matches the API response timestamp.
3. `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\credential.bin` exists, is non-readable as plaintext (`Get-Content credential.bin | Format-Hex` shows binary noise — that's DPAPI working).
4. Logs (per FR-014, ILogger configured at the GUI layer) include one **Information**-level entry "Dictionary fetched from API in <N>ms" and zero warnings.

**Now publish a change** in `stem-dictionaries-manager` (or update the WireMock stub), then quit and relaunch the app. The change must be visible in the dictionary content immediately on next launch.

## Scenario 2 — Cache fallback when the API is unreachable (US2)

**Validates**: FR-003, FR-007, FR-005 cached state, FR-014 distinct logging, SC-002 (offline operation).

```powershell
# Prerequisite: scenario 1 was run successfully so a valid cache + credential exist.
# Then take the API offline OR redirect the host to a black hole:
# Easiest in dev: stop the WireMock harness. In prod-like: disable network.

dotnet run --project src/GUI.WinForms
```

**Expected**:
1. App opens. Within ≤1s (SC-004 cached path), the dictionary state indicator (FR-005) shows **"Cached · <timestamp from scenario 1>"** with a colour or icon distinct from "Live".
2. The dictionary content matches what was cached in scenario 1 — verifiable by exercising any dictionary-dependent flow.
3. Logs include one **Warning**-level entry "Dictionary API unreachable, fell back to cache (reason: NetworkUnreachable)".
4. All panel-testing flows that worked in scenario 1 still work (FR-007).

**Variant 2a — auth failure** (FR-011f): bring the API back online but server-revoke the credential.

**Expected**:
1. State indicator shows **"Cached · <timestamp>"** with a UI signal distinct from the network-failure case (per CHK017 / FR-011f).
2. Logs include one **Error**-level entry under the auth-failure category (per FR-014 distinct levels).
3. `Refresh` button (FR-006) is enabled; clicking it does NOT re-attempt the same credential within the session (FR-011f); on next app launch, the credential store is cleared and re-provisioning runs.

## Scenario 3 — Manual refresh (US3)

**Validates**: FR-006 (GUI-exposed refresh control), FR-005 transitions Cached → Live, no behaviour change when already Live.

```powershell
# Prerequisite: scenario 2 in progress — app is running on cache because API was unreachable.
# Bring the API back online while the app is still running.

# In the running app, click the Refresh button (visible from the main view per FR-006).
```

**Expected**:
1. State indicator transitions **Cached → Live · just now** within ≤5s.
2. Cache file is overwritten with the fresh response (FR-004); `fetched_at` updates.
3. Logs: one **Information**-level "Dictionary refreshed by user, state Cached→Live".
4. Click Refresh again immediately while Live: state indicator briefly flickers ("Refreshing…"), then returns to **Live · just now** (the refresh succeeded again). No torn UI, no error.
5. Click Refresh while a fetch is already in flight: per CHK021, the second click is either coalesced with the first request or rejected with a "Refresh in progress" indication — implementation choice deferred to `/speckit-tasks`, but the requirement is "no duplicate concurrent fetches".

## Scenario 4 — First launch with no cache and no API (FR-008)

**Validates**: FR-008 (clear actionable error, app does not silently proceed).

```powershell
# Clean slate, network disabled, NO seed credential — full first-run-offline scenario.
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Stem.ButtonPanel.Tester" -ErrorAction SilentlyContinue
# Disable network or stop WireMock.

dotnet run --project src/GUI.WinForms
```

**Expected**:
1. App displays a modal error explaining: "Cannot start: dictionary unavailable and no offline cache exists. Restore network connectivity and relaunch, or contact support if this is your first run."
2. App exits cleanly with a non-zero exit code; no half-initialized window.
3. Logs include one **Error**-level entry under the FR-011d setup-failure category — distinct from FR-011f auth failures and FR-003 network failures.

## Scenario 5 — Concurrent app instances (Edge Case + FR-002)

**Validates**: FR-002 atomic write, no torn cache files.

```powershell
# Open two app instances simultaneously, both performing startup fetch.
# Easiest with Start-Process running twice in parallel:
Start-Process -FilePath "dotnet" -ArgumentList "run --project src/GUI.WinForms"
Start-Process -FilePath "dotnet" -ArgumentList "run --project src/GUI.WinForms"
```

**Expected**:
1. Both instances launch successfully and reach the Live state.
2. After both have settled, `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\dictionary.json` is a single, valid, parseable JSON file containing one of the two responses' payloads (not a torn mix).
3. No `dictionary.json.tmp` file remains in the directory (atomic-rename cleanup happened).

## What to look at during code review

- `JsonFileDictionaryCache.WriteAsync` — atomic write implementation (write-temp-then-rename or filesystem lock per FR-002). Must `fsync` the temp file before the rename so a crash mid-write doesn't lose the previous good cache.
- `HttpDictionaryClient` — single `HttpClient` instance with `Timeout = 5s` per FR-012; auth header set per request from `IInstallationCredentialStore`.
- `DictionaryService.RefreshAsync` — coalesce-or-reject behaviour for in-flight fetch + new manual refresh (CHK021). Confirm in tests.
- `DpapiCredentialStore` — `DataProtectionScope.CurrentUser` per R-2. `ClearAsync` must overwrite the file with random bytes before deleting (defense in depth, file recovery).
- Composition root — `IInstallationCredentialStore` registered as singleton; `IDictionaryProvider` registered with two named instances (or as keyed services); `DictionaryService` resolves both and orchestrates.
