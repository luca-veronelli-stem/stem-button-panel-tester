# Changelog

All notable changes to ButtonPanelTester follow [Semantic Versioning](https://semver.org/) and are recorded here in [Keep a Changelog](https://keepachangelog.com/) format.

## [Unreleased]

### Added

- **Dictionary-from-API (`feat/001-dictionary-from-api`)** — replace the embedded Excel dictionary with a live fetch from `stem-dictionaries-manager` plus an at-rest JSON cache fallback for offline / unreachable-API operation. New top-of-form indicator shows *live*, *cached (offline)*, *cached (credential problem)*, *cached (other)*; a Refresh button manually re-fetches without restarting. DPAPI-backed per-installation API key (per FR-011 family). Cross-link: `stem-dictionaries-manager#1` (server-side `/register` bootstrap, deferred per R-1), `stem-device-manager#94` (companion credential migration).
- New F# project beachheads for Phase 2/3 of the LANGUAGE migration: `src/Core.FSharp/` (domain types and interfaces) and `src/Services.FSharp/` (orchestration). Sibling-project pattern preserves Constitution VI's "no hybrid inside one project" rule. F# tests live in a sibling `tests/Tests.FSharp/`.
- Bumped `llm-settings` Standard pin to `v1.2.1`. Inline copies of the eight new content standards (`EVENTARGS`, `VISIBILITY`, `LOGGING`, `THREAD_SAFETY`, `CANCELLATION`, `COMMENTS`, `ERROR_HANDLING`, `CONFIGURATION`) plus the `README_TEMPLATE.md` and `STANDARD_TEMPLATE.md` doc templates landed under `docs/`.
- Bumped `llm-settings` Standard pin to `v1.3.1`. Inline copies under `docs/Standards/` regenerated; `BUILD_CONFIG.md`/`CI.md` pick up the v1.3.0 whitespace-only CI format-check change, `MIGRATION.md` picks up the v1.3.0 anti-pattern note and the v1.3.1 pre-fix-lockfile Pitfalls section. Locally-customized files (`CLAUDE.md`, `README.md`, `Directory.Packages.props`, `.github/workflows/ci.yml`, `.github/workflows/release.yml`) preserved as-is via the `v1.3.1` skip-local-edits guard.
- Filled in unfilled v1 template placeholders in `README.md` (Overview) and `CLAUDE.md` (Repo-specific notes, Language choices, Active migrations); fixed the double-period in the README badge (`.stem-standard.json` description now ends without a `.`, matching the template's `**{{Description}}.**` substitution).
- Replaced template-leaked project names (`ButtonPanelTester.Core`, etc.) in `README.md`'s Quick Start and Solution Structure with the actual `src/<Component>/` folders, and ported a corrected onion-direction project-dependency graph from the legacy `BUTTONPANEL_TESTER.md` snapshot.

### Fixed

- `LICENSE` no longer self-disclaims as a "DUMMY" notice and now carries the correct project name (`Stem.ButtonPanel.Tester` instead of the lifted-from-`stem-communication` "STEM Communication Protocol"). The license body is unchanged from what was already there.

### Changed

- Constitution Principle I (Standards-First) no longer hardcodes a Standard-version literal — references the `**Standard version:**` pin in `CLAUDE.md` indirectly. Constitution bumped to `v1.0.2` (PATCH; clarification only). Tracked upstream as [`luca-veronelli-stem/llm-settings#37`](https://github.com/luca-veronelli-stem/llm-settings/issues/37) for the cross-repo guidance note.

### Fixed

- Escalated the `ButtonPanelTestService` flake handling to **class-level** `Category=FlakyOnCi` on `ButtonPanelTestServiceTests`, `ButtonPanelTestServiceIntegrationTests`, and `ButtonPanelTestServiceE2ETests` after a 7th method (`TestAllAsync_Successful_Full_Test_All_Pass`) flaked on Windows CI. Per-method traits added in earlier PRs are kept (redundant but harmless) and will be cleaned up alongside the root-cause fix tracked in #3.

### Removed
