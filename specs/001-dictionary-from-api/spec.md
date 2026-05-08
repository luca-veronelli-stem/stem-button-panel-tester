# Feature Specification: Dictionary from stem-dictionaries-manager API

**Feature Branch**: `feat/001-dictionary-from-api`
**Created**: 2026-05-06
**Status**: Draft
**Input**: User description: "Replace the Excel-fed button panel dictionary with the stem-dictionaries-manager API as the authoritative source, falling back to a local JSON cache (refreshed on every successful API fetch) when the API is unreachable. Same general approach as stem-device-manager, but with a JSON cache instead of an .xlsx fallback."

## Clarifications

### Session 2026-05-08 — stopgap downgrade ⚠️

- **Decision**: ship a same-day API-backed build by adopting the
  `stem-device-manager` pattern (plaintext API key in configuration; auth
  header `X-Api-Key`) AND mapping onto the existing
  `stem-dictionaries-manager` endpoint surface (`/api/dictionaries/{id}/resolved`)
  rather than the speced `/v1/dictionary` endpoint that the server does not
  implement. Implemented on `feat/dictionary-api-key-config-stopgap`.
- **Spec parts deliberately violated**:
  - FR-011a (no plaintext credential on disk),
  - FR-011b (no manual supplier configuration step),
  - FR-011c (per-Installation revocation, no cross-install blast radius),
  - Constitution Principle I via the `CONFIGURATION` standard's "secret never
    in config",
  - the `panel_types`-tree contract documented in
    [`contracts/dictionary-api.md`](./contracts/dictionary-api.md) (replaced
    at runtime by a single-PanelType mapping over `DictionaryResolvedDto`),
  - per-variable `scaling` (hardcoded to `1.0` because the server's wire
    shape does not carry it).
- **Spec parts preserved**: the wire-level retry/log behaviour from FR-011d/e/f
  (401 → cache fallback, no retry, distinct log level) and the entire
  cache-fallback story from US2 (the cache envelope schema is independent
  of the wire shape).
- **Scope**: this build only. The DPAPI store, the `IInstallationCredentialStore`
  F# contract, and the DPAPI tests are retained on disk so re-securing is a
  recompose, not a re-implement.
- **Tracking**: [`docs/STOPGAP_API_KEY.md`](../../docs/STOPGAP_API_KEY.md) for
  the full waiver and re-secure plan; follow-up issue linked from there.

### Session 2026-05-06

- Q: Auth mechanism — API key, OAuth, mutual TLS, or same-as-`stem-device-manager`? → A: **API key**, but stored securely (not plaintext as in `stem-device-manager`'s current `appsettings.json` approach). Spec captures the security posture (encrypted-at-rest, no manual supplier setup, per-installation revocable). Concrete mechanism — bootstrap-exchange vs per-supplier installer + DPAPI — deferred to `/speckit-plan`.
- Q: Cache scope and storage location — per-user, per-machine, per-environment? → A: **Per-user, per-machine, single environment** at `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\dictionary.json`. Per-environment partitioning is **not** in scope for v1 but is a forward-compatible future change (FR-010 already mandates that an unreadable cache is treated as absent rather than as a hard error, so changing the path later is non-destructive).
- Q: API fetch timeout — 3s, 5s, 10s, configurable? → A: **5 seconds** (end-to-end). Matches SC-004's healthy-connection budget; on timeout the app behaves as if the API were unreachable and falls back to the cache (FR-003).
- Q: SC-006 baseline cycle time — what is it today? → A: Reframed. The SC is no longer "drop from X to Y" but expressed as a forward-looking contract: **automatic refresh on every app launch (already FR-001), plus an always-available GUI-exposed manual refresh (FR-006) for on-demand updates within a session**. No timer-driven background refresh, no Windows scheduled task — keep the architecture simple. The cost is that an app left open for many hours sees a fresh dictionary only when the user clicks Refresh or restarts; that trade-off is accepted explicitly.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Live dictionary from the API (Priority: P1)

A test technician launches the app on a workstation with network access. The app contacts `stem-dictionaries-manager` at startup, retrieves the current button panel dictionary, and uses it for the rest of the session. When dictionary maintainers publish a change in `stem-dictionaries-manager`, that change is visible to the technician on the next launch — no file copy, no redeploy, no manual import.

**Why this priority**: This is the whole point of the feature. Today, dictionary changes require a coordinated `.xlsx` rebuild and redistribution, which is the exact friction we are removing. Without P1, the feature delivers nothing.

**Independent Test**: With the API up and reachable, launch the app from a clean environment (no cache present), confirm the dictionary loads, and confirm a maintainer-side change in `stem-dictionaries-manager` is reflected on the next launch.

**Acceptance Scenarios**:

1. **Given** the API is reachable and no cache exists, **When** the app starts, **Then** it fetches the dictionary from the API and uses it for the session.
2. **Given** the API is reachable and a cache exists from a prior run, **When** the app starts, **Then** it fetches a fresh copy from the API and uses it (the cache is overwritten with the fresh response).
3. **Given** a maintainer publishes a dictionary change at `T0`, **When** the app is started after `T0`, **Then** the change is visible in the loaded dictionary.

---

### User Story 2 — Cache fallback when the API is unreachable (Priority: P1)

A test technician working at a remote site with intermittent connectivity launches the app. The API is unreachable (no network, VPN dropped, service down). The app uses the most recently cached dictionary so the technician can continue working with the dictionary content as it stood at the last successful fetch.

**Why this priority**: This is also P1. STEM techs work in the field where connectivity is unreliable; without a fallback, the new feature is strictly worse than today's Excel approach (Excel works offline by definition). P1 and P2 together are the MVP.

**Independent Test**: With a populated cache and the network disconnected, launch the app and confirm the dictionary loads from cache, the user is informed it's cached, and panel testing flows that depend on the dictionary continue to function.

**Acceptance Scenarios**:

1. **Given** a valid cache exists and the API is unreachable, **When** the app starts, **Then** it loads the dictionary from the cache and displays a "cached dictionary in use" indicator with the cache timestamp.
2. **Given** the API is reachable but returns an empty or malformed payload, **When** the app processes the response, **Then** it discards the bad payload, keeps the existing cache, and surfaces a warning.
3. **Given** the API is unreachable and no cache exists (clean install offline), **When** the app starts, **Then** it surfaces a clear error explaining the app cannot proceed without an initial successful API fetch and exits cleanly.

---

### User Story 3 — Cache freshness signaling and manual refresh (Priority: P2)

A test technician wants to know whether the dictionary they are working with is current or cached, and wants to manually refresh without restarting the app when they regain connectivity.

**Why this priority**: P2, not P1, because the MVP works without it — silent cache fallback is acceptable for first delivery as long as the cache state is recoverable on next launch. Visible state and manual refresh are quality-of-life additions that prevent confusion ("am I testing against the right dictionary?") and avoid unnecessary app restarts.

**Independent Test**: With the app running on a cache, restore connectivity and trigger the manual refresh control; confirm the indicator transitions from "cached" to "live" and the cache is updated.

**Acceptance Scenarios**:

1. **Given** the app is running on a cached dictionary, **When** the user views the dictionary state indicator, **Then** they see the cache's last-update timestamp.
2. **Given** the app is running on a cached dictionary and the API has become reachable, **When** the user triggers a manual refresh, **Then** the app fetches the live dictionary, updates the cache, and the indicator transitions to "live".
3. **Given** the app is running on a live dictionary, **When** the user triggers a manual refresh, **Then** the app re-fetches and confirms the dictionary is unchanged (or applies any new changes).

---

### Edge Cases

- **Cache schema drift**: a cache written by an older app version is unreadable by the current version. The app MUST treat an unreadable cache as "no cache present" (not as a hard error) and proceed to fetch from the API; if the API is also unavailable, behave as User Story 2 acceptance scenario 3.
- **Partial response**: the API returns a payload that is well-formed but missing fields the dictionary requires. Treat as malformed (User Story 2 acceptance scenario 2).
- **Authentication failure**: the API rejects the credentials. Treat as "API unreachable" for fallback purposes, but surface a distinct error message that points to credential configuration rather than network.
- **Concurrent app instances on the same machine**: if two instances start simultaneously and both fetch + write the cache, the cache file MUST end up consistent (one of the two responses wins; no torn writes). Behaviour is symmetric — neither instance is "primary".
- **Cache file corruption (disk-level)**: treated identically to schema drift — discarded silently, app proceeds as if no cache existed.
- **Slow API**: the fetch takes longer than a user is willing to wait. The app MUST not block startup indefinitely; after a defined timeout, behave as if the API were unreachable and fall back to cache.
- **Manual refresh while a fetch is already in flight**: the second request is either coalesced with the first or rejected with a "refresh in progress" indication; the app MUST NOT issue duplicate concurrent fetches.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST fetch the button panel dictionary from the `stem-dictionaries-manager` API on application startup.
- **FR-002**: System MUST persist the most recent successful API response as a local cache before the dictionary is consumed by other components. Cache writes MUST be **atomic** — write to a temp file then rename, or hold an exclusive filesystem lock — so concurrent writes from multiple app instances on the same machine cannot produce a torn cache file. The final on-disk state MUST contain the complete payload of one writer; partial mixes of two writers' payloads are forbidden.
- **FR-003**: System MUST fall back to the cached dictionary when the API call fails for any reason — connection refused, timeout exceeded, HTTP error, malformed payload, or authentication failure.
- **FR-004**: System MUST overwrite the cache on every successful API fetch so the cache is always the most recent good response.
- **FR-005**: System MUST display, somewhere in the application UI, whether the active dictionary was loaded from the API ("live") or from the cache ("cached"), including the timestamp of the cache when the cached path is taken.
- **FR-006**: System MUST expose a **manual dictionary refresh control directly in the GUI** (not buried in a settings dialog or menu archaeology) that triggers an immediate API fetch without restarting the app. The control MUST be visible from the main view.
- **FR-007**: System MUST function — open, run panel tests, exercise all dictionary-dependent flows — once a valid cache exists, regardless of network state.
- **FR-008**: System MUST report a clear, actionable error when no cache exists *and* the API is unreachable on first launch, and MUST NOT silently proceed with an empty or default dictionary.
- **FR-009**: System MUST source the runtime dictionary exclusively from the API or its cache; no other source (including `.xlsx` files) is consulted at runtime once this feature is active.
- **FR-010**: System MUST treat an unreadable cache (schema drift, corruption, version mismatch) as if no cache existed, with no user-facing crash.
- **FR-011**: System MUST authenticate to the `stem-dictionaries-manager` API using a per-installation **API key** as the **wire-level credential** (chosen over OAuth, mutual TLS, and basic auth for operational simplicity and alignment with the rest of the STEM fleet). The mechanism by which the API key arrives on the workstation — server-side bootstrap exchange against a `/register` endpoint, or unwrapping an installer-embedded encrypted bundle to encrypted-at-rest storage on first run — is a **provisioning concern** distinct from the wire-level authentication itself; the choice between mechanisms is finalized in `/speckit-plan`. Whichever mechanism is chosen MUST yield an API key that satisfies FR-011a/b/c and the lifecycle expectations in FR-011d/e/f.
- **FR-011a**: System MUST persist the API credential in encrypted-at-rest storage on the supplier's workstation. No plaintext credential MUST appear on disk during normal operation, including in installer artefacts left on disk after install.
- **FR-011b**: Initial credential provisioning MUST NOT require any manual configuration step by the supplier. **Concretely**, after the supplier double-clicks the installer the app MUST be functional with no further action: no environment variable to set, no UI prompt for a secret during install or first launch, no file the supplier is asked to edit, no value the supplier is asked to copy-paste from documentation. The installer MAY display informational messages but MUST NOT solicit any input that affects the credential. (CHK022/CHK023 resolution.)
- **FR-011c**: Per-**Installation** API credential revocation MUST be possible without redeploying the app, where an **Installation** (see Key Entities) is one Windows user account on one workstation that has completed credential provisioning. The server-side identity unit MUST be the same Installation unit used for the dictionary cache (FR-013) — one cache and one credential per Installation, no cross-talk. One supplier's compromised credential MUST NOT blast-radius to other installations even on the same machine if a different Windows user runs the app.
- **FR-011d**: Bootstrap / credential-provisioning failure on first launch (network unreachable, server error, bootstrap token rejected as expired, used, or supplier-mismatched) MUST be handled distinctly from ongoing-operation failures. The system MUST log the failure at a "setup-failure" level distinct from FR-014's network/auth levels, surface a user-facing error that distinguishes "initial setup couldn't complete" from FR-008's "no cache + no API on first launch", and refuse to proceed to other API calls (no point — they will all 401). Retry happens on the next launch or via an explicit user action; there is no automatic retry storm within a single launch.
- **FR-011e**: API credentials are in one of these lifecycle states: **Provisioned** (just issued by the bootstrap exchange or installer), **Active** (in use), **Rotated** (server-side replaced, possibly with a grace window), **Revoked** (server-side invalidated, immediate effect), and **Expired** (validity period exceeded — applicable only if the server-side policy uses time-bounded keys). The client treats Rotated, Revoked, and Expired identically at runtime — they all surface as a 401 from the API and trigger FR-011f. Recovery from any non-Active state is a re-provisioning event, governed by FR-011d's failure-handling rules when re-provisioning itself fails.
- **FR-011f**: On HTTP 401 (Unauthorized) from any API call, the system MUST: (i) treat the response as a failed fetch and engage the FR-003 cache-fallback path; (ii) NOT retry the same call with the same credential within the same session (no thrash); (iii) log the auth failure at the FR-014 distinct auth-failure level; (iv) surface a user-facing indicator that distinguishes "credential problem" from "network problem"; and (v) on the next launch or on explicit user-triggered manual refresh (FR-006), attempt re-provisioning per FR-011d if the chosen mechanism supports it, otherwise leave recovery to operations (reinstall).
- **FR-013**: System MUST store the dictionary cache under the **per-user local-app-data directory** of the Windows user running the app (concretely: `%LOCALAPPDATA%\Stem.ButtonPanel.Tester\dictionary.json`). Cache scope is one file per Windows user account per machine; the app targets a single `stem-dictionaries-manager` environment in v1 (per-environment partitioning is out of scope but MUST remain forward-compatible — adding an environment-named subdirectory to the path later MUST NOT break existing installations).
- **FR-014**: System MUST log every API fetch outcome (success, network failure, timeout, auth failure, malformed payload) and every cache-fallback event via `ILogger` per the project's `LOGGING` standard (`docs/Standards/LOGGING.md`). Auth failures MUST be logged at a distinct level/category from generic network failures so an ops review can distinguish "supplier credentials revoked / not yet provisioned" from "supplier offline".
- **FR-012**: System MUST apply a **5-second end-to-end timeout** to the API fetch (covering DNS, TCP, TLS, request, and response). On timeout the fetch is treated as a failure and the cache fallback path (FR-003) takes over. The timeout MUST NOT be silently exceeded — the user-facing flow either has a fresh dictionary, a cached dictionary, or the FR-008 "no cache + no API" error within roughly the SC-004 budget.

### Key Entities

- **ButtonPanelDictionary**: the in-memory representation of the dictionary the app uses for panel testing — list of variables, panel types, protocol mappings. Already exists in `Core`; this feature replaces its source, not its shape (modulo any mapping required by FR-001).
- **DictionaryCache**: a local artifact persisting the most recent successful dictionary response together with metadata: `fetched_at` timestamp, schema version, and the dictionary payload itself. Lifetime is per-user, per-machine.
- **DictionarySource**: a runtime indicator of where the active dictionary came from — `Live` (just fetched) or `Cached(timestamp)` (loaded from cache because the API was unavailable). Drives FR-005.
- **Installation**: one Windows user account on one workstation that has completed credential provisioning. The unit of identity for both the per-user-per-machine dictionary cache (FR-013) and the per-installation API credential (FR-011 family). Each Installation has its own DPAPI-protected (or equivalent encrypted-at-rest) credential and its own dictionary cache; revoking, rotating, or corrupting one Installation MUST NOT affect any other Installation, including different Windows accounts on the same machine.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a dictionary change is published in `stem-dictionaries-manager` at time `T0`, the change is reflected in the app on every launch after `T0` with no manual file step.
- **SC-002**: With the network disconnected and a populated cache, the app reaches a "ready to test" state in ≤ 3 seconds and supports the full set of panel-testing flows that work today on Excel.
- **SC-003**: A user can determine whether the active dictionary is live or cached, including the cache age, in ≤ 1 navigation step from the main screen (no menu archaeology).
- **SC-004**: Time from app launch to "dictionary loaded" is ≤ 5 seconds with a healthy API connection and ≤ 1 second from a populated cache.
- **SC-005**: Zero `.xlsx` files are read by the app at runtime once this feature is active in production. Excel-based loading code paths are either removed or guarded by a build/runtime flag that is off by default.
- **SC-006**: After this feature ships, a dictionary update no longer requires an app redeploy. From the moment a maintainer publishes a change in `stem-dictionaries-manager`, every supplier instance picks the change up on its **next app launch** (per FR-001 startup fetch), and any user can pull the change **immediately within a running session** via the GUI manual refresh control (FR-006). No file copy, no installer rerun, no scheduled task on the supplier's machine.

## Assumptions

- The `stem-dictionaries-manager` API exposes a single endpoint (or a small, well-defined set) that returns the dictionary content currently loaded from `.xlsx`. The shape is close enough to the existing `Core` types that mapping is a thin transformation, not a domain rewrite.
- Authentication and base-URL configuration for the API follow the same pattern used by `stem-device-manager` (the user's prior reference implementation).
- The cache is a single JSON file stored under the per-user app local data directory. JSON is chosen over alternatives (SQLite, binary, .xlsx) for human inspectability and trivial schema migration; this is a default that can be revisited in `/speckit-plan` if a non-JSON format is justified.
- The cache is per-user, per-machine, single environment in v1 (one cache file per Windows user account on the workstation; the app targets one `stem-dictionaries-manager` URL). Per-environment partitioning is out of scope for v1 but the path layout chosen MUST be forward-compatible (see FR-013).
- "Button panel dictionary" maps to a single API call — no per-panel-type fan-out.
- The existing Excel-loading code in `Data` (with the `-7155632` ARGB literal trick, see #28) is no longer required at runtime once this feature is active; whether it stays in the test suite for unit-test fixtures is a decision deferred to `/speckit-plan`.
- The transition is a hard cutover, not a feature-flagged dual-source mode. Once the API path is in production, the Excel path is disabled.

## Out of scope (this feature)

- Bidirectional sync (the app reads dictionaries; it does not push changes upstream).
- A UI for editing dictionaries inside this app.
- Migrating other STEM apps off Excel — `stem-device-manager` already uses the API; other repos are independent decisions.
- Caching anything other than the button panel dictionary (e.g. user settings, telemetry).
- Schema migration tooling — schema drift is handled by discarding the cache, not by transforming it.
