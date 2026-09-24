---
layout: default
title: MCP Server (agents)
parent: Features
nav_order: 8
---

# MCP Server — LogMyDay for AI agents

LogMyDay ships a built-in [Model Context Protocol](https://modelcontextprotocol.io) server. Any MCP-capable agent — Claude Code, Claude Desktop, or your own — can connect to your LogMyDay instance and work with your real data: log a value from a sentence, answer "how was my sleep last month", tick off a reminder, set up a tag. It runs inside the LogMyDay server at `/mcp`; there is nothing extra to install or host.

The MCP server is a separate thing from the in-app AI assistant. The assistant answers questions inside the app; the MCP server lets *your* agent act on your behalf, with a key you control.

## 1. Create an API key

1. Sign in to the web dashboard and open **Profile**.
2. In **API keys**, give the key a name, choose a scope and an optional expiry, and click **Create**.
3. Copy the token now — it is shown exactly once. Only its prefix (`lmd_xxxxxxxx`) is ever shown again.

Scopes:

| Scope | What the agent can do |
|---|---|
| **Read-only** | List and read everything you own, run summaries and exports. Write tools are not even listed. |
| **Read-write** | Everything above, plus create, update, complete, skip and delete. Admin tools additionally require your account to be an admin. |

Keys can be revoked from the same panel at any time; a revoked or expired key gets `401` on its next call. You can hold up to 20 active keys. Keys authenticate the MCP endpoint only — the REST API and the mobile app keep using your password.

## 2. Connect an agent

The profile panel shows both snippets below with your server address filled in.

**Claude Code**

```bash
claude mcp add --transport http logmyday https://your-host/mcp \
  --header "Authorization: Bearer lmd_…"
```

Run `/mcp` inside Claude Code to confirm `logmyday` is connected, then ask: *"Call server_info and tell me who I am."*

**Any MCP client (generic JSON)**

```json
{
  "mcpServers": {
    "logmyday": {
      "type": "http",
      "url": "https://your-host/mcp",
      "headers": { "Authorization": "Bearer lmd_…" }
    }
  }
}
```

The transport is Streamable HTTP, stateless: every request carries the key, nothing is cached on the server between calls.

Local development uses the ASP.NET Core development certificate; trust it (`dotnet dev-certs https --trust`) rather than disabling certificate checks.

## 3. What the agent can do

Tools are grouped by feature. Names are `verb_noun`; the agent sees a description for each and for every argument.

| Family | Tools |
|---|---|
| Server | `server_info` — versions, your key's scope, your time zone and culture, conventions, input types, enums. **Agents should call this first.** |
| Logging | `log_value` (the everyday tool: tag by name, value in plain form), `list_activities`, `get_activity`, `create_activity`, `update_activity`, `delete_activity`, `check_duplicate`, `get_period_sum`, `list_available_years`, `list_activities_by_year` |
| Tags | `list_tags`, `get_tag`, `find_tag`, `create_tag`, `update_tag`, `delete_tag` |
| Reference data | tag groups, option lists, colour schemes (list/get/create/update/delete each), `list_units`, `list_quantities`, `get_unit`, `create_unit`, `update_unit`, `delete_unit`, `list_input_types` |
| Reminders | `list_reminders`, `get_reminder`, `create_reminder`, `update_reminder`, `delete_reminder`, `complete_reminder`, `reopen_reminder`, `skip_reminder`, `unskip_reminder`, `reorder_reminders` |
| Todo lists | `list_todo_lists`, `get_todo_list`, `create_todo_list`, `update_todo_list`, `delete_todo_list`, `create_todo_item`, `update_todo_item`, `delete_todo_item`, `complete_todo_item`, `reopen_todo_item`, `skip_todo_item`, `unskip_todo_item`, `reorder_todo_items` |
| Day locks | `list_tag_day_locks`, `get_tag_day_lock`, `set_tag_day_lock`, `delete_tag_day_lock` |
| Scan mappings | `list_scan_mappings`, `get_scan_mapping`, `lookup_scan_code`, `create_scan_mapping`, `update_scan_mapping`, `delete_scan_mapping` |
| Analytics | `get_activity_summary` — totals, averages, streaks and per-day/week/month buckets for one tag |
| Event log | `query_event_log`, `count_events`, `record_event`, `purge_event_log` |
| Backup | `get_backup_info`, `export_secure_backup`, `validate_backup`, `restore_secure_backup`, `clear_user_data` |
| Excel | `preview_export`, `get_oldest_activity_date`, `generate_export` (the workbook comes back as an embedded file) |
| You | `get_me`, `update_my_preferences` |
| Admin | `admin_list_users`, `admin_create_user`, `admin_update_user`, `admin_delete_user`, `admin_reset_password`, `admin_get_ai_settings`, `admin_update_ai_settings`, `admin_export_backup`, `admin_clear_all_data` |

**Resources** (for clients that attach context rather than call tools): `logmyday://tags`, `logmyday://tags/{id}`, `logmyday://tag-groups`, `logmyday://option-lists`, `logmyday://units`, `logmyday://input-types`, `logmyday://me`.

**Prompts** — ready-made instructions the client can offer as slash commands:

- `daily_review` (`date` optional) — what you logged that day, open reminders and todos, required tags left empty. Changes nothing.
- `weekly_summary` (`weekEnding`, `tags` optional) — per-tag statistics for the last seven days compared with the seven before.
- `log_by_conversation` — turn "I ran 5 km and slept 7 hours" into `log_value` calls, confirming each one.

Not exposed on purpose: the in-app AI assistant, sign-in and password flows, and the legacy multipart backup import.

### Naming a tag

Wherever a tool takes a `tag`, the agent can pass what you said:

| You write | Meaning |
|---|---|
| `42` | the tag with id 42 |
| `Vitamin D` | exact name; otherwise the single tag that starts with it; otherwise the single tag that contains it |
| `Health:Vitamin D` | the tag *Vitamin D* in the group *Health* |
| `:Vitamin D` | the ungrouped tag *Vitamin D* |

When several tags match, `find_tag` returns the candidates and the agent is expected to ask you.

## 4. Conventions the agent follows

**Values.** A value is sent as a plain number, string or boolean and encoded for the tag's input type exactly as the web app stores it:

| Input type | Encoding |
|---|---|
| Integer | whole number |
| Decimal | number with a dot, stored with two decimals |
| Boolean | `true`/`false`; `yes`/`no` and `1`/`0` are accepted |
| Date | `yyyy-MM-dd` |
| Time | `HH:mm` (24-hour) |
| Star ratings, scores, percentage | whole number within the type's range |
| Text with an option list | one of the list's values (the display name is accepted too) |

**Dates.** `date` arguments are `yyyy-MM-dd`. Date-times are your local wall-clock time (`yyyy-MM-ddTHH:mm`) — the same naive local values LogMyDay stores; a value with a time-zone offset is converted into your time zone first. "Today" and "now" mean today and now in the time zone on your profile.

**Accumulation.** Logging to a non-repeatable numeric tag that already has a value in the period *adds* to it (`action: accumulated`) — the same rule the app applies. `log_value` with `mode: set` replaces the period's value instead.

**Summaries.** `get_activity_summary` buckets rows by their stored date with no time-zone conversion (`convention: stored-local-date`), so its numbers match the Insights pages. A day "has data" when at least one row exists (for Yes/No tags: at least one *true*); the current streak may end yesterday if today has nothing yet.

**Paging.** List tools return pages of at most 200 rows (default 50).

**Errors.** A failed call returns `{ code, message, … }` where `code` is one of `not-found`, `invalid`, `conflict`, `tag-day-locked` (with the tag, the date and a hint), `confirmation-required` (see below), `forbidden`, `rate-limited`, `too-large` or `error`.

## 5. Destructive actions and confirmation

Deleting a single activity, reminder, todo item or scan mapping needs no ceremony. Actions that destroy more than one thing take a `confirm` argument that must equal a fixed sentinel; without it the tool refuses, reports what *would* happen (`impact`) and changes nothing. A well-behaved agent shows you that impact and asks before confirming.

| Tool | Sentinel | Why |
|---|---|---|
| `delete_tag` | `DELETE_TAG_<id>` | deletes every activity ever logged for the tag |
| `delete_unit` | `DELETE_UNIT_<id>` | units are shared by all users |
| `delete_todo_list` | `DELETE_LIST_<id>` | deletes its items |
| `restore_secure_backup` | `RESTORE_LMD_BACKUP` | merges a backup into your account |
| `clear_user_data` | `CLEAR_MY_DATA` | deletes all your data |
| `purge_event_log` | `PURGE_EVENTS` | only when deleting the whole log |
| `admin_delete_user` | `DELETE_USER_<email>` | deletes an account and everything it owns |
| `admin_clear_all_data` | `CLEAR_ALL_USERS_DATA` | every user's data |

Destructive tools are also limited to 10 calls per minute per key, on top of the endpoint's 120 requests per minute.

Some writes have side effects the descriptions spell out: completing a reminder or a todo item logs an activity to its completion tag, skipping a reminder logs a zero/false row, and reopening never removes what was logged. A locked day (`tag-day-locked`) is your decision — agents are told to stop and ask rather than unlock.

## 6. Audit trail

Every write made through a key is recorded in your **Event Log** as `MCP <tool> via <key prefix>: ok|error`, with the arguments attached (passwords, tokens and backup payloads redacted). Filter the Event Log by type **MCP agent** to see only these rows. Reads are not recorded. Entries an agent writes with `record_event` are prefixed `Agent:` so they never look like audit rows.

## 7. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `401 Unauthorized`, `WWW-Authenticate: Bearer` | no key, an unknown, revoked or expired key, or five bad attempts from your address in 15 minutes | create a new key on Profile; check the header is exactly `Authorization: Bearer lmd_…` |
| `403 Forbidden` on connect | signed in with a session cookie or Basic auth — `/mcp` accepts API keys only | use a key |
| `Access forbidden: This tool requires authorization` | a read-only key calling a write tool, or a non-admin calling an `admin_*` tool | create a read-write key, or ask an admin |
| `429 Too Many Requests` | more than 120 requests per minute from this key | wait a minute |
| `rate-limited` in a tool result | more than 10 destructive calls per minute | wait a minute |
| `too-large` | an export bigger than the inline cap (1 MB for backups, 5 MB for workbooks) | use the download on the web page named in the hint |
| the agent is redirected to a login page | you are calling a path other than `/mcp` | check the URL |
| numbers differ from a chart | a different date convention | summaries use the stored date, like Insights; `server_info` states every convention |

## 8. Smoke test script

After connecting a fresh key with Claude Code, this conversation exercises the whole path:

1. *"Call server_info."* — expect your e-mail, time zone, the key scope and the schema version.
2. *"Find the tag Vitamin D."* — expect a match or a candidate list.
3. *"Log 2000 for Vitamin D today."* — expect `action: created` (or `accumulated` if you already logged today).
4. *"List today's activities."* — the row is there.
5. *"Summarise Vitamin D for the last 30 days."* — count, sum, streaks, one bucket per day.
6. Run the `daily_review` prompt — a short review, nothing changed.
7. *"Delete the tag Vitamin D."* — the agent must report `confirmation-required` with the activity count and ask; say no.
8. Revoke the key on Profile — the next call returns `401`.
9. Repeat step 1 with a **read-only** key — `tools/list` contains no `create_*`, `update_*`, `delete_*`, `log_value` or `admin_*` tools.

## 9. Architecture notes (for developers)

- **Project** `src/LogMyDay.Mcp` (net9.0) on the official `ModelContextProtocol.AspNetCore` SDK, referenced by `LogMyDay.App` and mounted with `AddLogMyDayMcp()` and `MapLogMyDayMcp()`. Tools call the application services directly — no HTTP loopback. Layering: Domain ← Shared ← Api ← Mcp ← App.
- **Authentication.** The `smart-auth` scheme routes `Bearer lmd_…` — and any request under `/mcp` without Basic credentials — to the `api-key` handler, which hashes the token (SHA-256), compares it in constant time and emits the standard claims plus `lmd_auth`, `lmd_scope`, `lmd_api_key_id` and `lmd_api_key_prefix`. Policies `McpRead`, `McpWrite` and `McpAdmin` require an API-key principal, so a browser cookie can never reach `/mcp` (no CSRF surface). Failed attempts share the progressive lockout of Basic auth.
- **Authorization** is declarative: `[Authorize(Policy = …)]` on tool classes and methods, enforced by the SDK's authorization filters, which also hide tools a key cannot call from `tools/list`. A call-tool filter additionally refuses non-read-only tools on read-only keys, meters destructive tools (10/min per key), maps every service exception to a coded error result and writes the audit row.
- **Rate limiting.** `UseRateLimiter` runs after authentication; policy `mcp` is a sliding window of 120/min partitioned by key id (by IP when unauthenticated). MCP traffic never draws from the REST `api` bucket.
- **Serialisation** uses the same `JsonSerializationSettings` as REST (camelCase, enums as names), so tool schemas and results match the API's wire format. Enum arguments come from the Domain enums — one source of truth.
- **Schema version.** `McpSchemaVersion.Current` (`server_info.mcpSchemaVersion`) is `1.0`; minor bumps are additive, major bumps rename or remove.
- **Tests.** `McpToolContractTests` reflects over every tool (explicit annotations, snake_case names, policy invariants, sentinel parameters, registration, prompt references); `LogMyDay.Api.IntegrationTests/Mcp*Tests` drive the endpoint through the SDK client with seeded keys.
- **Reverse proxies** must forward the `Authorization` header and must not buffer `/mcp` responses (server-sent events) — see [Docker deployment](../setup/docker-deployment.md).

See also: [CLI Tool](cli.md) for scripted, non-agent access, and [Users](users.md) for the API-keys panel.
