# Changelog

All notable changes to ButtonPanelTester follow [Semantic Versioning](https://semver.org/) and are recorded here in [Keep a Changelog](https://keepachangelog.com/) format.

## [Unreleased]

### Changed

- ⚠️ **Dictionary — stopgap (credential + endpoint).** Triple bypass to ship a same-day API-backed build against the actual `stem-dictionaries-manager` deployment used by `stem-device-manager`: (1) DPAPI-backed credential store and first-run bootstrap step bypassed — API key is now read directly from `Dictionary:ApiKey` (override via env var `Dictionary__ApiKey`; supplier-specific value lives in gitignored `appsettings.Production.json`); (2) wire-level credential sent as `X-Api-Key` instead of `Authorization: Bearer`; (3) endpoint is `GET /api/dictionaries/{DictionaryId}/resolved` (default `DictionaryId = 2` — "Pulsantiere") instead of the speced `GET /v1/dictionary`, with the response mapped to a single-`PanelType` `ButtonPanelDictionary` client-side. `Variable.Scaling` is hardcoded to `1.0` because the server's wire shape doesn't carry that field — variables that should be scaled will display raw values. Violates FR-011a/b/c, the `panel_types` contract in `specs/001-dictionary-from-api/contracts/dictionary-api.md`, and Constitution Principle I (`CONFIGURATION` standard); waivers documented in [`docs/STOPGAP_API_KEY.md`](./docs/STOPGAP_API_KEY.md). `DpapiCredentialStore.cs` and the `IInstallationCredentialStore` F# contract are retained on disk so the re-secure follow-up is a recompose, not a re-implement.

### Added

- **Dictionary-from-API (`feat/001-dictionary-from-api`)** — replace the embedded Excel dictionary with a live fetch from `stem-dictionaries-manager` plus an at-rest JSON cache fallback for offline / unreachable-API operation. New top-of-form indicator shows *live*, *cached (offline)*, *cached (credential problem)*, *cached (other)*; a Refresh button manually re-fetches without restarting. DPAPI-backed per-installation API key (per FR-011 family). Cross-link: `stem-dictionaries-manager#1` (server-side `/register` bootstrap, deferred per R-1), `stem-device-manager#94` (companion credential migration).
- New F# project beachheads for Phase 2/3 of the LANGUAGE migration: `src/Core.FSharp/` (domain types and interfaces) and `src/Services.FSharp/` (orchestration). Sibling-project pattern preserves Constitution VI's "no hybrid inside one project" rule. F# tests live in a sibling `tests/Tests.FSharp/`.
- Bumped `llm-settings` Standard pin to `v1.2.1`. Inline copies of the eight new content standards (`EVENTARGS`, `VISIBILITY`, `LOGGING`, `THREAD_SAFETY`, `CANCELLATION`, `COMMENTS`, `ERROR_HANDLING`, `CONFIGURATION`) plus the `README_TEMPLATE.md` and `STANDARD_TEMPLATE.md` doc templates landed under `docs/`.
- Bumped `llm-settings` Standard pin to `v1.3.1`. Inline copies under `docs/Standards/` regenerated; `BUILD_CONFIG.md`/`CI.md` pick up the v1.3.0 whitespace-only CI format-check change, `MIGRATION.md` picks up the v1.3.0 anti-pattern note and the v1.3.1 pre-fix-lockfile Pitfalls section. Locally-customized files (`CLAUDE.md`, `README.md`, `Directory.Packages.props`, `.github/workflows/ci.yml`, `.github/workflows/release.yml`) preserved as-is via the `v1.3.1` skip-local-edits guard.
- Bumped `llm-settings` Standard pin to `v1.3.2`. `docs/Standards/MIGRATION.md` regenerated to pick up the expanded Pitfalls recipe (`-Force` + `git checkout HEAD --` for substantively-customized files). First clean re-run on this repo since the v1.3.1 lockfile heal -- no `-Force` needed.
- Filled in unfilled v1 template placeholders in `README.md` (Overview) and `CLAUDE.md` (Repo-specific notes, Language choices, Active migrations); fixed the double-period in the README badge (`.stem-standard.json` description now ends without a `.`, matching the template's `**{{Description}}.**` substitution).
- Replaced template-leaked project names (`ButtonPanelTester.Core`, etc.) in `README.md`'s Quick Start and Solution Structure with the actual `src/<Component>/` folders, and ported a corrected onion-direction project-dependency graph from the legacy `BUTTONPANEL_TESTER.md` snapshot.
- Aligned `specs/001-dictionary-from-api/contracts/register-api.md` with the server-side `/speckit-clarify` decisions of 2026-05-07 in [`stem-dictionaries-manager#1`](https://github.com/luca-veronelli-stem/stem-dictionaries-manager/issues/1) (closes #48): unversioned `/register` path, JSON-body bootstrap token (no `Authorization` header), camelCase fields, unified `401 { error: "registration failed" }` failure body, non-idempotent (no `409 already_registered` re-issuance), no `expiresAt` on the success body, `stbt_` / `stak_` token-format prefixes. Resolved consumer-side Q2: keep deterministic unsalted SHA-256 hashing of `machineId` / `osUserId`. Cross-references in `research.md` (R-9 path versioning) and `data-model.md` (Installation wire mapping) updated accordingly.

### Fixed

- `LICENSE` no longer self-disclaims as a "DUMMY" notice and now carries the correct project name (`Stem.ButtonPanel.Tester` instead of the lifted-from-`stem-communication` "STEM Communication Protocol"). The license body is unchanged from what was already there.

### Changed

- Constitution Principle I (Standards-First) no longer hardcodes a Standard-version literal — references the `**Standard version:**` pin in `CLAUDE.md` indirectly. Constitution bumped to `v1.0.2` (PATCH; clarification only). Tracked upstream as [`luca-veronelli-stem/llm-settings#37`](https://github.com/luca-veronelli-stem/llm-settings/issues/37) for the cross-repo guidance note.

### Fixed

- Escalated the `ButtonPanelTestService` flake handling to **class-level** `Category=FlakyOnCi` on `ButtonPanelTestServiceTests`, `ButtonPanelTestServiceIntegrationTests`, and `ButtonPanelTestServiceE2ETests` after a 7th method (`TestAllAsync_Successful_Full_Test_All_Pass`) flaked on Windows CI. Per-method traits added in earlier PRs are kept (redundant but harmless) and will be cleaned up alongside the root-cause fix tracked in #3.

### Removed
