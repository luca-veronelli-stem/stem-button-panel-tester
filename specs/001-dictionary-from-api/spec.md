# Feature Specification: Dictionary from stem-dictionaries-manager API

**Feature Branch**: `feat/dictionary-from-api`
**Created**: 2026-05-06
**Status**: Draft
**Input**: User description: "Replace the Excel-fed button panel dictionary with the stem-dictionaries-manager API as the authoritative source, falling back to a local JSON cache (refreshed on every successful API fetch) when the API is unreachable. Same general approach as stem-device-manager, but with a JSON cache instead of an .xlsx fallback."

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
- **FR-002**: System MUST persist the most recent successful API response as a local cache before the dictionary is consumed by other components.
- **FR-003**: System MUST fall back to the cached dictionary when the API call fails for any reason — connection refused, timeout exceeded, HTTP error, malformed payload, or authentication failure.
- **FR-004**: System MUST overwrite the cache on every successful API fetch so the cache is always the most recent good response.
- **FR-005**: System MUST display, somewhere in the application UI, whether the active dictionary was loaded from the API ("live") or from the cache ("cached"), including the timestamp of the cache when the cached path is taken.
- **FR-006**: System MUST allow the user to trigger a manual dictionary refresh from the UI without restarting the app.
- **FR-007**: System MUST function — open, run panel tests, exercise all dictionary-dependent flows — once a valid cache exists, regardless of network state.
- **FR-008**: System MUST report a clear, actionable error when no cache exists *and* the API is unreachable on first launch, and MUST NOT silently proceed with an empty or default dictionary.
- **FR-009**: System MUST source the runtime dictionary exclusively from the API or its cache; no other source (including `.xlsx` files) is consulted at runtime once this feature is active.
- **FR-010**: System MUST treat an unreadable cache (schema drift, corruption, version mismatch) as if no cache existed, with no user-facing crash.
- **FR-011**: System MUST authenticate to the `stem-dictionaries-manager` API using [NEEDS CLARIFICATION: same mechanism as stem-device-manager — API key, OAuth, basic auth? Confirmed in /speckit-clarify].
- **FR-012**: System MUST apply a configurable timeout to the API fetch [NEEDS CLARIFICATION: default timeout — 3s? 5s? 10s? — to be defined in /speckit-clarify or /speckit-plan].

### Key Entities

- **ButtonPanelDictionary**: the in-memory representation of the dictionary the app uses for panel testing — list of variables, panel types, protocol mappings. Already exists in `Core`; this feature replaces its source, not its shape (modulo any mapping required by FR-001).
- **DictionaryCache**: a local artifact persisting the most recent successful dictionary response together with metadata: `fetched_at` timestamp, schema version, and the dictionary payload itself. Lifetime is per-user, per-machine.
- **DictionarySource**: a runtime indicator of where the active dictionary came from — `Live` (just fetched) or `Cached(timestamp)` (loaded from cache because the API was unavailable). Drives FR-005.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a dictionary change is published in `stem-dictionaries-manager` at time `T0`, the change is reflected in the app on every launch after `T0` with no manual file step.
- **SC-002**: With the network disconnected and a populated cache, the app reaches a "ready to test" state in ≤ 3 seconds and supports the full set of panel-testing flows that work today on Excel.
- **SC-003**: A user can determine whether the active dictionary is live or cached, including the cache age, in ≤ 1 navigation step from the main screen (no menu archaeology).
- **SC-004**: Time from app launch to "dictionary loaded" is ≤ 5 seconds with a healthy API connection and ≤ 1 second from a populated cache.
- **SC-005**: Zero `.xlsx` files are read by the app at runtime once this feature is active in production. Excel-based loading code paths are either removed or guarded by a build/runtime flag that is off by default.
- **SC-006**: After this feature ships, a dictionary update no longer requires an app redeploy; the elapsed time from "maintainer publishes change" to "users see change" drops from `[NEEDS CLARIFICATION: current cycle time, e.g. days]` to `≤ 1 app restart`.

## Assumptions

- The `stem-dictionaries-manager` API exposes a single endpoint (or a small, well-defined set) that returns the dictionary content currently loaded from `.xlsx`. The shape is close enough to the existing `Core` types that mapping is a thin transformation, not a domain rewrite.
- Authentication and base-URL configuration for the API follow the same pattern used by `stem-device-manager` (the user's prior reference implementation).
- The cache is a single JSON file stored under the per-user app local data directory. JSON is chosen over alternatives (SQLite, binary, .xlsx) for human inspectability and trivial schema migration; this is a default that can be revisited in `/speckit-plan` if a non-JSON format is justified.
- The cache is per-user (not shared between users on the same machine) and per-environment (dev / staging / prod use distinct caches).
- "Button panel dictionary" maps to a single API call — no per-panel-type fan-out.
- The existing Excel-loading code in `Data` (with the `-7155632` ARGB literal trick, see #28) is no longer required at runtime once this feature is active; whether it stays in the test suite for unit-test fixtures is a decision deferred to `/speckit-plan`.
- The transition is a hard cutover, not a feature-flagged dual-source mode. Once the API path is in production, the Excel path is disabled.

## Out of scope (this feature)

- Bidirectional sync (the app reads dictionaries; it does not push changes upstream).
- A UI for editing dictionaries inside this app.
- Migrating other STEM apps off Excel — `stem-device-manager` already uses the API; other repos are independent decisions.
- Caching anything other than the button panel dictionary (e.g. user settings, telemetry).
- Schema migration tooling — schema drift is handled by discarding the cache, not by transforming it.
