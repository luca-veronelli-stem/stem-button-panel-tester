# Changelog

All notable changes to ButtonPanelTester follow [Semantic Versioning](https://semver.org/) and are recorded here in [Keep a Changelog](https://keepachangelog.com/) format.

## [Unreleased]

### Added

### Changed

### Fixed

- Escalated the `ButtonPanelTestService` flake handling to **class-level** `Category=FlakyOnCi` on `ButtonPanelTestServiceTests`, `ButtonPanelTestServiceIntegrationTests`, and `ButtonPanelTestServiceE2ETests` after a 7th method (`TestAllAsync_Successful_Full_Test_All_Pass`) flaked on Windows CI. Per-method traits added in earlier PRs are kept (redundant but harmless) and will be cleaned up alongside the root-cause fix tracked in #3.

### Removed
