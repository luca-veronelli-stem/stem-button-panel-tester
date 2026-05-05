# Changelog

All notable changes to ButtonPanelTester follow [Semantic Versioning](https://semver.org/) and are recorded here in [Keep a Changelog](https://keepachangelog.com/) format.

## [Unreleased]

### Added

### Changed

### Fixed

- `CLAUDE.md` and `.stem-standard.json` now correctly stamp `v1.1.1` (matching the actual standards version applied in PR #1) instead of stale `v1.1.0`. Filled in the migration tracker placeholder with PR #1 + 2026-05-05.
- Tagged 5 more `ButtonPanelTestService`-related tests with `Category=FlakyOnCi` after they intermittently failed across Windows and Linux runners (`TestButtonsAsync_All_Buttons_Pass`, `E2E_ButtonPress_RealCanManagerAndProtocolDecodingWorks`, `E2E_LedTest_PartialFailure_UserRejectsSome`, `TestAllAsync_ProtocolManagerProcessesButtonEvents`, `TestAllAsync_DifferentPanelTypes_UseCorrectButtonCount`). Tracked alongside the original flake in #3.

### Removed
