# Changelog

All notable changes to this project will be documented in this file. The format roughly follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Release dates use the Europe/Prague timezone (UTC+01:00 during winter).
## Unreleased

### Added

  - Color Schemes: define reusable named schemes that map rating, score, percentage and integer values (exact values or ranges) to colors, assign one per tag, and see them applied across Activities, the Insights Calendar and Linear Calendar, and the shared rating displays. Ships direction-aware default colors out of the box (stars and percentage higher = better; scores lower = better) with per-tag overrides.

## v0.6.0 — 2026‑07‑08

### Added

  - Todo Lists v1 with per-list reminders
  - Reminders: boolean toggle for required tags, inline star/score pickers (numbered circles for score), per-day reminder state, background notifications with tap-to-reminder navigation and fulfilled-tag suppression
  - Event Log audit system with per-user event tracking, plus download, delete, filtering, sorting, and category/type filter by message prefix
  - `lmd` CLI tool
  - AI assistant in the mobile app with shared quick questions and markdown rendering
  - Full-width layout toggle next to the theme switcher
  - Quick-buttons navigation with smart visibility and a tag description field

### Changed

  - Upgraded Tailwind CSS from v3 to v4
  - Native WebView interop replacing browser JS interop paths
  - Security and logging hardening
  - Backup now applies streak compression to backup activities
  - Reminder scheduling arms the next occurrence and uses `setAlarmClock` so morning alarms fire; monitoring uses a date-range window
  - Quiet client-side SignalR circuit reconnect; raised the circuit receive-message cap

### Fixed

  - Event Log date filter now covers whole local days converted to UTC
  - Zero/empty activities are no longer logged
  - Mobile error handling and database provider guards
  - Layout constraints restored on nav and error UI; markdown helper in Assistant; wide-layout override for Tailwind v4 breakpoint class names
  - Resolved high-severity npm vulnerabilities in `src/ui`

## v0.5.0 — 2026‑03‑21

### Added

  - Barcode and QR code scanning
  - Tag groups
  - SQLite database support alongside SQL Server
  - Day navigation and save-and-next-day in activity modals

### Changed

  - Server URL is auto-detected; removed hardcoded `Api:BaseAddress` and password-reset URL
  - Optimized data loading performance

## v0.4.0 — 2026‑02‑24

### Added

  - AI integration

### Changed

  - v0.4.0 audit refactoring improvements

### Fixed

  - Separate tag selections for Excel and HTML reports
  - HTML reports now include all data

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