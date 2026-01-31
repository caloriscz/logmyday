# Changelog

All notable changes to this project will be documented in this file. The format roughly follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Release dates use the Europe/Prague timezone (UTC+01:00 during winter).
## v0.3.0 — 2026‑01‑31

### Added

  - Reports page with dedicated UI for data export features (Excel, CSV, backup)
  - Charts page with interactive line charts for visualizing numeric tag data over time
  - Multi-tag chart comparison for analyzing multiple metrics simultaneously
  - Per-tag chart type selection for customized visualizations
  - Correlation insights for discovering relationships between different tags
  - Integration test infrastructure with CustomWebApplicationFactory for ExportService testing
  - New icons for Excel export functionality

### Changed

  - Build system updated to run integration tests separately from unit tests
  - Excel export and backup UI refactored for improved user experience
## v0.2.0 — 2026‑01‑13

### Added

  - InputType editable flags (`IsRangeEditable`, `IsMinimumEditable`, `IsMaximumEditable`, `IsStepEditable`, `IsRepeatableEditable`) to control tag field editability in UI
  - InputType descriptions for better UI hints
  - Three new InputTypes: Rating 1-5, Rating 1-10, Percentage
  - Insights page for activity analytics
  - Journal page for daily activity overview
  - Statistics page for numeric tag analysis
  - New Icon component in LogMyDay.UI
  - InputTypeIds constants and InputTypeDefaults helper classes

### Fixed

  - Empty migration file (20260106191926_AddInputTypeEditableFlags) now contains proper schema changes
  - Excel export now properly filters by user ID
  - Backup.razor `GetAvailableTags` now passes required userId parameter

### Changed

  - TagCreate and TagEdit pages updated to respect InputType editable flags
  - DataSeeder extended with new InputTypes and editable flag values

## v0.1.0 — 2025‑11‑28

### Added

  - Initial public version