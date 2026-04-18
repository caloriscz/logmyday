---
layout: default
title: CLI Tool (lmd)
parent: Features
nav_order: 7
---

# CLI Tool — lmd

`lmd` is a .NET global tool that lets you interact with any LogMyDay server from the terminal. It is aimed at power users who prefer text-based workflows, developers building integrations, and automated pipelines that push data into LogMyDay without a browser.

## Installation

```bash
dotnet tool install -g LogMyDay.Cli
lmd --help
```

## Account management

`lmd` supports multiple named accounts backed by the Windows Credential Manager. Credentials are never stored in plain text.

```bash
# Add accounts
lmd login --server https://cloud.example.com --username alice@example.com --alias prod
lmd login --server https://localhost:5001 --username admin@test.com --alias local

# Switch active account
lmd use prod

# Check connection and active identity
lmd whoami

# List all stored accounts
lmd accounts

# Remove an account
lmd logout --alias local
```

A small `~/.lmd/config.json` file tracks the active alias only.

## Backup

```bash
# Download a full backup as JSON
lmd backup export
# → lmd-backup-2026-04-12.json in the current directory

lmd backup export -o /backups/my-backup.json

# Restore from a backup file
lmd backup import my-backup.json
```

## Reports (Excel)

```bash
# Date presets
lmd report export --preset last-month
lmd report export --preset last-quarter
lmd report export --preset last-year

# Custom date range
lmd report export --from 2026-01-01 --to 2026-01-31

# Specific tags only
lmd report export --preset last-month --tags 3,7,12

# Custom output path
lmd report export --preset last-month -o ~/reports/jan.xlsx
```

## Activities

### Listing and viewing

```bash
# List recent activities (default: 50 per page)
lmd activities list

# Filter by tag, date range, or description text
lmd activities list --tag "Body Weight"
lmd activities list --from 2026-04-01 --to 2026-04-12
lmd activities list --search "morning"

# Show a single activity by ID
lmd activities show 42

# JSON output (pipe-friendly)
lmd activities list --json
```

### Adding and editing

```bash
# Add an activity
lmd activities add --tag "Body Weight" --date 2026-04-12 --value 82.5
lmd activities add --tag "Steps" --date 2026-04-12T08:00:00 --value 9200

# Edit an activity (partial update — omitted flags keep their current value)
lmd activities edit 42 --value 83.0
lmd activities edit 42 --date 2026-04-13

# Delete an activity
lmd activities delete 42
lmd activities delete 42 --yes   # skip confirmation
```

Tag name matching is fuzzy: `--tag "weight"` will match "Body Weight" if no other tag contains the word.

### Batch import

```bash
# Import from CSV (dry-run first)
lmd activities import import.csv --dry-run
lmd activities import import.csv

# Import from JSON
lmd activities import import.json
```

**CSV format** — with header row:

```
tag,value,date,description
Body Weight,82.5,2026-04-01,
Steps,9200,2026-04-01,morning walk
Body Weight,82.0,2026-04-02,
```

Positional format (no header) is also accepted: `tag,value,date,description`.

**JSON format:**

```json
[
  { "tag": "Body Weight", "value": "82.5", "date": "2026-04-01" },
  { "tag": "Steps",       "value": "9200", "date": "2026-04-01", "description": "morning walk" }
]
```

## Tags

```bash
# List all tags
lmd tags list

# Filter by group or name
lmd tags list --group "Health"
lmd tags list --search "weight"

# Show full details for a tag (unit, type, limits, default)
lmd tags show "Body Weight"
lmd tags show 7
```

## Extensions

Extensions are scripts in any language that receive LogMyDay credentials via environment variables and interact with the API themselves.

### Extension manifest — `~/.lmd/extensions/<name>/extension.json`

```json
{
  "name":        "lastfm",
  "version":     "1.0.0",
  "description": "Import Last.fm play counts as daily activities",
  "command":     "python",
  "args":        ["lastfm.py"],
  "platforms":   ["windows", "linux", "macos"]
}
```

### Environment variables injected at runtime

| Variable | Value |
|----------|-------|
| `LMD_SERVER` | Active server URL |
| `LMD_USERNAME` | Active account username |
| `LMD_TOKEN` | Base64-encoded `username:password` (for HTTP Basic Auth headers) |
| `LMD_ALIAS` | Active account alias |

### Extension commands

```bash
lmd extensions list
lmd extensions show lastfm
lmd extensions install ~/scripts/lastfm/
lmd extensions run lastfm
lmd extensions remove lastfm
```

## JSON output

Every command that produces tabular output supports `--json` for pipe-friendly structured output:

```bash
lmd activities list --json | jq '.[] | {id, tag: .primaryTagName, value: .primaryTagValue}'
lmd tags list --json | jq '.[] | select(.groupName == "Health")'
```
