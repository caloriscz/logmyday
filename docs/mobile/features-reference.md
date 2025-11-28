---
layout: default
title: Features Reference
parent: Mobile Guide
nav_order: 2
---

# Features Reference

The Android app mirrors the web experience while adding mobile-only conveniences. Use this page as a quick refresher when you pick the project back up after a break or to understand what the companion app offers before installing it.

## Quick Activities

- Open **Settings → Quick Activities** to create, edit, or delete buttons.
- Pick the tag the button should log and, if you like, pre-fill the value you use most often.
- Buttons support numbers, text, booleans, dates, times, and ranges—whatever the tag expects.
- Definitions live in secure platform storage, so they survive app restarts and work even if you set them up while offline.
- Tapping a quick button logs the activity instantly with the current timestamp and activates a 15-second cooldown so you do not double-submit by accident. A disabled button displays an hourglass and *Cooling down…* text.

## Add Activity Modal

- Tap the floating action button (bottom-right) to open the modal from anywhere in the app.
- Launching it from **Notifications** or the **Activities** page keeps the tag and date you were viewing so you never lose context.
- Turn on **Add Another** when you are logging a batch of similar entries—the modal stays open and resets the form after each save.
- The app checks for missing or duplicate information before submitting, then confirms the entry once the server accepts it.
- Native Android date and time pickers appear automatically, so you never wrestle with desktop-style widgets on your phone.

## Notifications

- After you sign in, the app quietly checks for required tags you have not logged today and posts a reminder if any are missing.
- Tapping the reminder opens the correct tag ready to fill in. Swiping it away is fine if you already completed the task.
- Monitoring pauses automatically when you sign out or lose connection.
- Need more detail? See [Notifications](notifications.html) for a deeper explanation and advanced tips.

## Theme and Layout

- Switch between light and dark modes using the palette icon in the top bar; the choice sticks between sessions.
- Layout spacing follows the same responsive design as the web app, so logs, modals, and lists resize cleanly on phones and tablets.
- If the UI ever looks out of date after an upgrade, go to **Settings → About** and tap **Refresh UI Assets** to reload the latest styles.

## Connectivity Resets

- Logging out fully clears the saved connection so you can safely point the app at another server without restarting it.
- The login screen only accepts HTTPS addresses; if you mistype the URL the app highlights it before you connect.
- Need to verify the connection? Open the three-dot menu on **Home** and choose **Refresh Connection** to ping the server on demand.
