namespace LogMyDay.Api.Infrastructure.Data;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Which snapshots stay in the snapshot folder. A snapshot is named
/// <c>&lt;database&gt;-&lt;yyyyMMdd-HHmmss&gt;-before-&lt;what&gt;.db</c>: before a restore of a
/// user's database (<c>before-restore</c>), or before a migration (<c>before-&lt;migration&gt;</c>).
/// Files with other names are never touched.
/// </summary>
public static class SnapshotRetention
{
    /// <summary>The snapshots before a restore that each user keeps.</summary>
    public const int RestoreSnapshotsKept = 3;

    /// <summary>The snapshots before a migration that each database keeps: one per upgrade.</summary>
    public const int UpgradeSnapshotsKept = 2;

    private const string Restore = "restore";

    /// <summary>Removes a user's snapshots before a restore, but the newest ones.</summary>
    /// <returns>The removed files.</returns>
    public static IReadOnlyList<string> PruneRestoreSnapshots(string snapshotFolder, Guid userId, ILogger logger)
    {
        return Prune(Snapshots(snapshotFolder, userId.ToString("D")).Where(s => s.Before == Restore), RestoreSnapshotsKept, logger);
    }

    /// <summary>Removes a database's snapshots before a migration, but the newest ones.</summary>
    /// <param name="databaseName">The database file's name without its extension.</param>
    /// <returns>The removed files.</returns>
    public static IReadOnlyList<string> PruneUpgradeSnapshots(string snapshotFolder, string databaseName, ILogger logger)
    {
        return Prune(Snapshots(snapshotFolder, databaseName).Where(s => s.Before != Restore), UpgradeSnapshotsKept, logger);
    }

    /// <summary>Removes every snapshot of a user's database, when the user is deleted.</summary>
    /// <returns>The removed files.</returns>
    public static IReadOnlyList<string> RemoveAll(string snapshotFolder, Guid userId, ILogger logger)
    {
        return Prune(Snapshots(snapshotFolder, userId.ToString("D")), keep: 0, logger);
    }

    private static IEnumerable<Snapshot> Snapshots(string snapshotFolder, string databaseName)
    {
        if (!Directory.Exists(snapshotFolder))
        {
            return [];
        }

        var pattern = new Regex($"^{Regex.Escape(databaseName)}-(\\d{{8}}-\\d{{6}})-before-(.+)\\.db$", RegexOptions.CultureInvariant);

        return Directory.EnumerateFiles(snapshotFolder)
            .Select(path => (Path: path, Match: pattern.Match(Path.GetFileName(path))))
            .Where(file => file.Match.Success)
            .Select(file => new Snapshot(file.Path, file.Match.Groups[1].Value, file.Match.Groups[2].Value));
    }

    private static IReadOnlyList<string> Prune(IEnumerable<Snapshot> snapshots, int keep, ILogger logger)
    {
        var removed = new List<string>();
        var old = snapshots
            .OrderByDescending(s => s.Time, StringComparer.Ordinal)
            .ThenByDescending(s => s.Path, StringComparer.Ordinal)
            .Skip(keep);
        foreach (var snapshot in old)
        {
            // Housekeeping: a file that can't be removed now goes with a later snapshot.
            try
            {
                File.Delete(snapshot.Path);
                removed.Add(snapshot.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not remove the snapshot {Snapshot}", snapshot.Path);
            }
        }

        if (removed.Count > 0)
        {
            logger.LogInformation("Removed snapshots: {Snapshots}", string.Join(", ", removed.Select(Path.GetFileName)));
        }

        return removed;
    }

    private sealed record Snapshot(string Path, string Time, string Before);
}
