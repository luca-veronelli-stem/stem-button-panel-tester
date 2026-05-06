# Changelog

All notable changes to ButtonPanelTester follow [Semantic Versioning](https://semver.org/) and are recorded here in [Keep a Changelog](https://keepachangelog.com/) format.

## [Unreleased]

### Added

- Bumped `llm-settings` Standard pin to `v1.2.1`. Inline copies of the eight new content standards (`EVENTARGS`, `VISIBILITY`, `LOGGING`, `THREAD_SAFETY`, `CANCELLATION`, `COMMENTS`, `ERROR_HANDLING`, `CONFIGURATION`) plus the `README_TEMPLATE.md` and `STANDARD_TEMPLATE.md` doc templates landed under `docs/`.
- Filled in unfilled v1 template placeholders in `README.md` (Overview) and `CLAUDE.md` (Repo-specific notes, Language choices, Active migrations); fixed the double-period in the README badge (`.stem-standard.json` description now ends without a `.`, matching the template's `**{{Description}}.**` substitution).
- Replaced template-leaked project names (`ButtonPanelTester.Core`, etc.) in `README.md`'s Quick Start and Solution Structure with the actual `src/<Component>/` folders, and ported a corrected onion-direction project-dependency graph from the legacy `BUTTONPANEL_TESTER.md` snapshot.

### Fixed

- `LICENSE` no longer self-disclaims as a "DUMMY" notice and now carries the correct project name (`Stem.ButtonPanel.Tester` instead of the lifted-from-`stem-communication` "STEM Communication Protocol"). The license body is unchanged from what was already there.

### Changed

### Fixed

- Escalated the `ButtonPanelTestService` flake handling to **class-level** `Category=FlakyOnCi` on `ButtonPanelTestServiceTests`, `ButtonPanelTestServiceIntegrationTests`, and `ButtonPanelTestServiceE2ETests` after a 7th method (`TestAllAsync_Successful_Full_Test_All_Pass`) flaked on Windows CI. Per-method traits added in earlier PRs are kept (redundant but harmless) and will be cleaned up alongside the root-cause fix tracked in #3.

### Removed
