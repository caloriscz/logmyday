---
layout: default
title: Users
parent: Features
nav_order: 5
---

# Users

LogMyDay supports both solo journaling and shared deployments. Administrators can invite family members or teammates, manage roles, and reset passwords while everyday users stay in control of their profile and preferences.

## Admin capabilities

- Invite new users directly from the web dashboard and decide whether they should have admin rights.
- Manage culture, language, and time zone defaults so shared dashboards stay consistent.
- Review active users, disable accounts that no longer need access, and trigger password resets if someone forgets their credentials.

## Self-service profile management

- Every user can update their display name, preferred culture, and time zone without contacting an administrator.
- Change your password, regenerate recovery codes, and manage notification preferences from either the web or mobile app.
- Personal settings sync automatically so the mobile companion reflects your latest choices.

## API keys for agents

- The **API keys** panel on your Profile page creates keys for the built-in [MCP server](mcp.md), so an AI agent such as Claude Code can read and write your data on your behalf.
- Each key has a name, a scope (**read-only** or **read-write**) and an optional expiry. The token is shown once, at creation; afterwards only its prefix is visible. Revoke a key at any time — its next call is refused.
- Keys work for the MCP endpoint only. Signing in to the web or mobile app still uses your password, and a key can never create or revoke keys or change your password.
- Everything an agent writes with a key is recorded in your Event Log under the type **MCP agent**, with the key prefix.

## Security at a glance

- The web dashboard signs you in with secure cookies; the mobile app uses HTTPS and never stores your password on the device.
- Role-based permissions ensure regular users can only see their own data, while admins get elevated options for management tasks.
