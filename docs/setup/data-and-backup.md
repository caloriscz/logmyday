---
layout: default
title: Data, Backup and Upgrade
parent: Setup and Installation
nav_order: 3
---

# Data, Backup and Upgrade

LogMyDay keeps everything in SQLite files in one folder, the **data folder**. No database server is involved. This page says what is in the folder, how to back it up and restore it, and what the upgrade to v0.8.0 does.

## The data folder

The data folder is the folder of the file that `ConnectionStrings:DefaultConnection` names (`DB_PATH` in Docker). Without a connection string it is `data` in the application's folder. The startup log names the file in use: `Database: SQLite, <full path>`.

| Path | What it is |
| --- | --- |
| `logmyday.db` | The **platform database**: accounts, API keys, password resets and the installation's settings. |
| `users/<user id>.db` | One database per user, with everything that user logged and set up, including their units and their event log. |
| `snapshots/` | Copies that LogMyDay takes on its own before it changes a database: before a migration, and before a restore. |
| `logmyday.before-v0.8.0.db` | The original database of an installation upgraded from v0.7.2. See [Upgrading from v0.7.2](#upgrading-from-v072). |
| `split-work/`, `restore-work/` | Work folders, empty unless an upgrade or a restore is running. |

SQLite keeps `-wal` and `-shm` files next to a database while it is open. They belong to the database.

Creating a user creates their database. Deleting a user deletes it. A user's rows never leave their own file, so one user's backup, restore or clear cannot touch another user's data.

Give the application write access to the whole folder, not only to `logmyday.db`. On a hosted installation, put the folder outside the site's folder, so that a deployment that syncs the site cannot delete it.

## Backup

### Your own data

On the **Backup** page:

- **Database File → Download Database** downloads your database as one SQLite file. It is a complete copy: everything you logged and set up.
- **Export Data** downloads a JSON file with your tags, activities and the rest of what you set up. Use it to move data to another account or another installation.

### The whole installation (admins)

**Backup → Export Whole Installation** downloads a zip archive with the platform database and every user's database, at the paths they have in the data folder. Keep the archive private: it contains password and API key hashes.

Copying the data folder works as well, with the application stopped. While the application runs, a plain copy can miss what SQLite still holds in its `-wal` files; the archive and the downloads are consistent copies.

### What LogMyDay keeps on its own

Files in `snapshots/` are taken before a migration (`<name>-<time>-before-<migration>.db`) and before a restore (`<user id>-<time>-before-restore.db`). LogMyDay removes the older ones itself:

- Each database keeps the snapshots of its last two upgrades. A migration that fails changes nothing, so when it is tried again, the new snapshot replaces the one of the failed attempt instead of counting as another upgrade.
- Each user keeps the three newest snapshots before a restore. The older ones go right after a restore, so restoring again and again doesn't fill the disk.
- The snapshot just taken always stays, even when the server's clock went back.
- Deleting a user deletes their snapshots with their database.

Other files in the folder are left alone. Copy a snapshot elsewhere if you want to keep it for longer.

## Restore

### Your own data

**Backup → Database File → Restore Database** replaces your database with a file you downloaded earlier. Everything logged since the file was taken is replaced.

The file is checked before anything changes. It is refused when it is not a LogMyDay user database, when it comes from a newer version of LogMyDay, or when it holds another account's data. A file from an older version is migrated first. The replaced database is kept in `snapshots/`, among the user's three newest snapshots before a restore.

A database file restores only into the account it was taken from. To bring data into a different account, import a JSON export.

### The whole installation

1. Stop the application.
2. Empty the data folder, or point the connection string at a new, empty folder.
3. Unpack the archive into it, so that `logmyday.db` and `users/` sit directly in the folder.
4. Start the application.

The accounts, passwords, API keys and settings come with the archive. The stored AI key is encrypted with the installation's data protection keys (`DataProtection-Keys`), so on another server, move those keys as well or enter the AI key again.

### An admin, for another user

An admin can download and restore any user's database through the API: `GET /api/backup/database/{userId}` and `POST /api/backup/database/{userId}/restore` (the file as the form field `file`).

## Upgrading from v0.7.2

Up to v0.7.2 an installation was one database file with every user's data. v0.8.0 splits that file the first time it starts:

1. Back up the database file yourself before you upgrade.
2. Make sure the installation runs **v0.7.2 on SQLite**. An older version is refused with a message to install v0.7.2 first. An installation on SQL Server moves to SQLite on v0.7.2 (Backup → Export Whole Database); in Docker, see [Upgrading a v0.7.2 installation on SQL Server](docker-deployment.md#upgrading-a-v072-installation-on-sql-server).
3. Install v0.8.0 and start it. It builds the platform database and one database per user next to the original file, compares the number of rows of every table, and only then puts the new files in place.
4. The original file stays in the data folder as `logmyday.before-v0.8.0.db`. Keep it until you have checked the upgraded installation; then you can delete it.

The split needs free disk space of about twice the size of the database. If anything fails, the original file stays where it was, nothing new is left behind, and the application does not start; the log says what failed. If a failure comes while the files are being put in place and a step cannot be taken back, for example because another program holds a file, the log and the error say what to rename or remove by hand before the next start.

What changes for users:

- Shared ("global") option lists no longer exist. Each user whose tags used a shared list gets a copy of their own; a shared list nobody used goes to the first admin.
- Units, quantities and input types are each user's own.
- Rows that belonged to a deleted user are not carried over. They are listed in the log and stay in the original file.

To go back to v0.7.2, stop the application, remove `logmyday.db`, `users/` and `snapshots/`, rename `logmyday.before-v0.8.0.db` back to `logmyday.db`, and install v0.7.2. Whatever was logged on v0.8.0 is lost.
