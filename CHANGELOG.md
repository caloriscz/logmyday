# Changelog

All notable changes to this project will be documented in this file. The format roughly follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/). Release dates use the Europe/Prague timezone (UTC+01:00 during winter).
## Unreleased

### Added

  - Backup → **Database File**: download your data as one SQLite file, and restore it into the same account later. A restore checks the file first (a LogMyDay user database, of this version or an older one, with this account's data only) and keeps the replaced database in the `snapshots` folder. Also `GET /api/backup/database` and `POST /api/backup/database/restore`; an admin reaches any user's database with `GET /api/backup/database/{userId}` and `POST /api/backup/database/{userId}/restore`.
  - Backup → **Export Whole Installation**, for admins: a zip archive with the platform database and every user's database. Unpacked into an empty data folder, LogMyDay runs on it. Also `GET /api/backup/export/installation`. It replaces v0.7.2's single-file export (`GET /api/backup/export/sqlite`).
  - A default place for the database: without a connection string it is `data/logmyday.db` in the application's folder. A relative `Data Source` counts from the application's folder on every host, and the startup log names the file in use.

### Changed

  - **LogMyDay runs on SQLite only.** `Database:Provider` is optional and must be `Sqlite` if set. Docker uses `DB_PATH` and the `logmyday-data` volume instead of the SQL Server settings and secrets.
  - The local deployment (Cake, Web Deploy) keeps the `data` folder on the server, next to `DataProtection-Keys`.

### Removed

  - **SQL Server support.** This version does not start on SQL Server: a configuration that still points at it stops the start and says how to move. **Before upgrading**, move the installation on v0.7.2: export the database (Backup → Export Whole Database), put the file where the app can write, set `Database:Provider` to `Sqlite` and `ConnectionStrings:DefaultConnection` to `Data Source=<path to the file>`, and restart. Migrations no longer come with SQL Server scripts.

### Fixed

  - Clearing your data removes everything you logged and set up: reminders, colour schemes, day locks and the event log stayed before, and the API and MCP clear also left todo lists. The units and input types stay.
  - An admin's clear and backup of another user's data (`DELETE /api/backup/clear`, the MCP tools `admin_clear_all_data` and `admin_export_backup`) work on that user's data.
  - A new installation starts on an empty SQLite file. Before, the oldest migrations failed on SQLite, so only databases created by older versions worked.

## v0.7.2 — 2026‑11‑20

### Fixed

  - The Backup page's **Danger Zone** clears only your own data. Before, it deleted the data of every user on the installation, together with the shared input types and patterns. The warning and the confirmation now say what is deleted and that other users' data stays.
  - The version in the navigation menu shows the app's real version; it had stayed at 0.7.0.

### Removed

  - The GitHub Actions deployment that ran on every tag. It built without the server's configuration and deleted `appsettings.json` on the hosting, so the site failed to start. Deployment runs locally with the Cake build (`build/README.md`).

## v0.7.1 — 2026‑11‑20

### Fixed

  - A tag a trigger reminds you to log can no longer become the computed tag of a rule, also not through a backup import. Completing or skipping that trigger's reminders failed with a server error. A trigger whose tag is already computed sets no reminder and says so in the Event Log.
  - A tag or record field that a rule, check or trigger uses keeps its type and option list; changing either is refused with the name of the relation, as deleting the tag is. Before, the change went through and the relation stopped working without a word. The tag edit pages on the web and mobile show the reason.
  - Completing a reminder or todo item whose value set a trigger's reminder now reports it like any other save: the save notice on the web and mobile, `reminders` in the REST and MCP (`complete_reminder`, `complete_todo_item`) results, and the phone arms the alarm at once.
  - Completing a reminder or todo item with a record no longer fails around midnight when the app runs in another time zone than the user (for example a web server in UTC). Today's completion now uses the same moment as the record form's default time instead of noon of the app's date.

## v0.7.0 — 2026‑11‑17

### Added

  - MCP server: LogMyDay now speaks the Model Context Protocol at `/mcp`, so AI agents such as Claude Code can work with your data — tools covering logging (`log_value` resolves a tag by name and encodes the value for its input type), tags and reference data, reminders, todo lists, day locks, scan mappings, a server-side `get_activity_summary` (totals, streaks, day/week/month buckets), event log, backup, Excel export, profile and admin; `logmyday://` resources; and three prompts (`daily_review`, `weekly_summary`, `log_by_conversation`). Destructive tools require a confirmation sentinel and report their impact first; every write is recorded in the Event Log under the new **MCP agent** type. See `docs/features/mcp.md`.
  - API keys: create read-only or read-write keys with optional expiry on the Profile page; the token is shown once, keys can be revoked, and they authenticate the MCP endpoint only. Keyed requests get their own rate-limit bucket (120/min per key).
  - Insights → Timeline Comparison: a new web view that lays two to five tags out as horizontal timeline rows over the same day columns. The first row is the reference; later rows either follow its dates or sit at a day offset from them, so relationships like coffee today against sleep the following night, or this year against the same period last year, can be read off directly. Each row picks its own aggregation (first value, sum, average, count, maximum), the visible period is 7/14/30/90 days with day, week, month, year and whole-period navigation, and cells reuse the existing tag color schemes.
  - Color Schemes: define reusable named schemes that map rating, score, percentage and integer values (exact values or ranges) to colors, assign one per tag, and see them applied across Activities, the Insights Calendar and Linear Calendar, and the shared rating displays. Ships direction-aware default colors out of the box (stars and percentage higher = better; scores lower = better) with per-tag overrides. A Yes/No scheme colors yes/no tags on every surface.
  - Tag Activity Relations, rules (Tags → Rules, web and mobile): a computed tag gets its daily value from other tags — a weighted sum (with negative factors), the average, minimum, maximum or count of the day's entries, a fixed value on days a tag is logged, or a value chosen by ordered conditions. Values follow every save, past days are filled through a recompute with a preview, computed values are marked and read-only everywhere (Activities, Calendar, Charts, Statistics, Excel, CLI, MCP), rules travel in backups and are recomputed after a restore, and MCP gets `list_tag_rules` / `get_tag_rule`.
  - Tag Activity Relations, checks: "If A …, then B …" checks on the same day, with the shortcuts **Not on the same day** and **Requires**. The **Rule review** (web and mobile) lists the days that break them. A **Warn** check also warns right after a save that breaks it: a notice on the web and mobile, and `warnings` in the REST, MCP (`log_value`, `create_activity`, `update_activity`, `log_record`, `update_record`) and CLI results. The value is saved anyway. MCP: `list_tag_checks`, `get_tag_check`, `review_tag_checks`.
  - Tag Activity Relations, triggers: logging a tag (or a value condition on it, e.g. "Alcohol is yes") sets a one-day reminder to log another tag or record type, a set time after the entry or at a time of day (that day or the next). The reminder is withdrawn when the condition stops holding and closes itself when the prompted tag is logged. The save notice says what was set and when; the phone arms the alarm right after its own save; save results over REST, MCP and the CLI carry `reminders`. MCP: `list_tag_triggers`, `get_tag_trigger`.
  - Records: one measurement made of several values at one moment (e.g. blood pressure). Record types with up to 20 fields, logged and shown as one entry on the web and mobile (Add activity, Quick, Activities, Journal), completion targets for reminders and todos (completing opens the record form), one Excel sheet per record type, MCP record tools and CLI output. Fields stay ordinary tags for charts, insights and rules.

### Changed

  - Mobile reminders: a reminder outside its monitoring window is no longer armed; the fire-time check shows the reminder's current text and works after a cold start, so reminders completed on the web stay silent. **Mark Done** in a notification opens the value dialog when the reminder needs a value, instead of logging an empty entry.
  - Activity edits enforce the tag's limits and the period total like new entries; the minimum value is enforced; edits and deletes are written to the Event Log; reminder skips no longer add repeated zero rows; adding to an existing value no longer depends on the server's culture.
  - Activity times are stored in the user's local time everywhere, including reminder and todo auto-logs; day locks use the stored local date.
  - Mobile Basic-auth requests reuse a recent password verification instead of running the full password hash on every request.
  - Mobile diagnostics recording is opt-in for admins (Diagnostics page).
  - Rate limiting is partitioned per user, and per key for API keys.

### Fixed

  - An activity, day lock, reminder or todo list can no longer reference another user's tag.
  - A request for an id that does not exist returns 404 instead of 500.
  - Journal edits no longer shift the time by the time-zone offset.

### Database

  - New migrations: `AddColorSchemes`, `AddApiKeys`, `AddTagRules`, `AddTagRuleTemplates`, `AddRecords`, `AddTagChecks`, `AddTagTriggers`. SQLite migrates on start; SQL Server needs the matching scripts from the wiki (`migrations/`), run in date order before deploying; each skips itself when already applied.

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