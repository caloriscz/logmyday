namespace LogMyDay.Api.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Brings one database file to this version's schema with its context's migrations. A database
/// that already holds data is copied into the snapshot folder first, and the database keeps the
/// snapshots of its last upgrades only (<see cref="SnapshotRetention.PruneUpgradeSnapshots"/>).
/// </summary>
public static class DatabaseUpgrade
{
    /// <summary>The folder of the snapshots, next to the platform database.</summary>
    public const string SnapshotFolderName = "snapshots";

    /// <summary>
    /// Applies the pending migrations. A new database is created by them. A failure names the
    /// file and, when one was taken, the snapshot.
    /// </summary>
    /// <returns>The snapshot's path, or null when none was needed.</returns>
    public static string? Migrate(DbContext context, string snapshotFolder, ILogger logger)
    {
        var path = DatabaseLocation.GetFilePath(context.Database.GetConnectionString())
            ?? throw new NotSupportedException("Only a database in a file can be migrated.");

        var pending = context.Database.GetPendingMigrations().ToList();
        if (pending.Count == 0)
        {
            return null;
        }

        string? snapshot = null;
        try
        {
            var known = context.Database.GetMigrations().ToHashSet();
            var applied = File.Exists(path) ? context.Database.GetAppliedMigrations().ToList() : [];

            if (!applied.Any(known.Contains))
            {
                if (HasTables(context, path))
                {
                    throw new InvalidOperationException(
                        "The file holds tables that this version's migrations didn't create. It comes from an older LogMyDay, and this version can't upgrade it in place.");
                }
            }
            else
            {
                snapshot = TakeSnapshot(context, path, snapshotFolder, pending[0]);
                logger.LogInformation("Snapshot of {Path} before migrating: {Snapshot}", path, snapshot);
            }

            context.Database.Migrate();
            logger.LogInformation("Migrated {Path}: {Migrations}", path, string.Join(", ", pending));
        }
        catch (Exception ex)
        {
            var hint = snapshot == null ? string.Empty : $" Its state before the migration is in {snapshot}.";

            throw new InvalidOperationException($"The database {path} could not be migrated.{hint} {ex.Message}", ex);
        }
        finally
        {
            // Also after a failure: a start that fails again and again would otherwise add a copy
            // of the database each time.
            if (snapshot != null)
            {
                SnapshotRetention.PruneUpgradeSnapshots(snapshotFolder, Path.GetFileNameWithoutExtension(path), snapshot, logger);
            }
        }

        return snapshot;
    }

    private static bool HasTables(DbContext context, string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        // EF's own tables (the migration history and its lock) don't count: a first migration
        // that failed leaves them behind.
        return context.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite~_%' ESCAPE '~' AND name NOT LIKE '~_~_EF%' ESCAPE '~'")
            .AsEnumerable()
            .Single() > 0;
    }

    private static string TakeSnapshot(DbContext context, string path, string snapshotFolder, string firstPending)
    {
        Directory.CreateDirectory(snapshotFolder);

        var snapshot = Path.Combine(snapshotFolder, SnapshotRetention.NewName(Path.GetFileNameWithoutExtension(path), firstPending));

        // VACUUM INTO writes a consistent copy, whatever is still in the write-ahead log.
        context.Database.ExecuteSqlRaw("VACUUM INTO {0};", snapshot);

        return snapshot;
    }
}
