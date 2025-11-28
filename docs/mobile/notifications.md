---
layout: default
title: Notifications
parent: Mobile Guide
nav_order: 3
---

# Notifications

LogMyDay keeps you on track with gentle reminders when required activities are still missing. The MAUI Android app sends native notifications so you do not have to keep the screen open.

## How it works

- **Automatic monitoring** – After you sign in, LogMyDay checks the server for required tags that have not been filled for today.
- **Smart cadence** – The check runs every 30 seconds in debug builds and every five minutes in production builds, so you receive timely but not overwhelming reminders.
- **Privacy aware** – Monitoring pauses as soon as you sign out or the app loses its server connection. No polling happens while you are logged out.
- **Actionable alerts** – Tapping a notification opens the relevant tag context inside the app so you can log the missing activity immediately.

## Viewing and clearing reminders

1. Unlock your device and pull down the notification shade.
2. Tap the LogMyDay entry to jump straight into the activity modal for the highlighted tag.
3. Log the activity and the reminder disappears automatically. You can also swipe the notification away if you already handled the task elsewhere.

## Tips for power users

- **Quick re-check** – Open the three-dot menu on the Home page and choose *Refresh requirements* to run the monitoring step on demand.
- **Cooldown awareness** – Quick Activity buttons still honour their 15-second cooldown. If you see a notification, wait for the button to re-enable before logging another entry.
- **Adjusting frequency** – Advanced users can tune the interval in configuration files, but the defaults are safe for daily use.

Remember that notifications rely on your server being reachable from the device. If reminders stop appearing, verify that the API is running and the mobile app is still connected (Settings → Server Connection).
