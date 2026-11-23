namespace LogMyDay.Api.Infrastructure.Data;

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Names the snapshots and decides which stay in the snapshot folder. A snapshot is named
/// <c>&lt;database&gt;-&lt;time&gt;-before-&lt;what&gt;.db</c>: before a restore of a user's database
/// (<c>before-restore</c>), or before a migration (<c>before-&lt;migration&gt;</c>). Files with
/// other names are never touched. Removing snapshots is housekeeping: it never fails the
/// operation that wrote one.
/// </summary>
public static class SnapshotRetention
{
    /// <summary>The snapshots before a restore that each user keeps.</summary>
    public const int RestoreSnapshotsKept = 3;

    /// <summary>The snapshots before a migration that each database keeps: one per upgrade.</summary>
    public const int UpgradeSnapshotsKept = 2;

    /// <summary>What a snapshot before a restore comes before.</summary>
    public const string BeforeRestore = "restore";

    /// <summary>
    /// The time in a new snapshot's name, in UTC. Earlier versions wrote it to the second, without
    /// the milliseconds; such names still count.
    /// </summary>
    private const string TimeFormat = "yyyyMMdd-HHmmss-fff";

    /// <summary>The name of a new snapshot of a database, taken now.</summary>
    /// <param name="databaseName">The database file's name without its extension.</param>
    /// <param name="before">What the snapshot comes before: <see cref="BeforeRestore"/> or a migration.</param>
    public static string NewName(string databaseName, string before)
    {
        return $"{databaseName}-{DateTime.UtcNow.ToString(TimeFormat, CultureInfo.InvariantCulture)}-before-{before}.db";
    }

    /// <summary>
    /// After a restore: the user keeps the snapshot just taken and their newest others, up to
    /// <see cref="RestoreSnapshotsKept"/>.
    /// </summary>
    /// <param name="justTaken">
    /// The snapshot the restore wrote. It always stays, even when the clock went back and an
    /// older snapshot carries a later time.
    /// </param>
    /// <returns>The removed files.</returns>
    public static IReadOnlyList<string> PruneRestoreSnapshots(string snapshotFolder, Guid userId, string justTaken, ILogger logger)
    {
        return Housekeeping(logger, () =>
        {
            var snapshots = Snapshots(snapshotFolder, userId.ToString("D")).Where(s => s.Before == BeforeRestore).ToList();
            if (!snapshots.Any(s => s.Is(justTaken)))
            {
                return [];
            }

            var old = Newest(snapshots.Where(s => !s.Is(justTaken))).Skip(RestoreSnapshotsKept - 1);

            return Remove(old, logger);
        });
    }

    /// <summary>
    /// After a migration, whether it succeeded or not: the database keeps the snapshot just taken
    /// and the newest snapshot of each of its last upgrades before it, up to
    /// <see cref="UpgradeSnapshotsKept"/>. Snapshots before the same migration show the same state,
    /// as a migration that fails changes nothing, so a retry replaces the snapshot of the failed
    /// attempt instead of counting as another upgrade.
    /// </summary>
    /// <param name="databaseName">The database file's name without its extension.</param>
    /// <param name="justTaken">The snapshot the migration wrote. It always stays.</param>
    /// <returns>The removed files.</returns>
    public static IReadOnlyList<string> PruneUpgradeSnapshots(string snapshotFolder, string databaseName, string justTaken, ILogger logger)
    {
        return Housekeeping(logger, () =>
        {
            var snapshots = Snapshots(snapshotFolder, databaseName).Where(s => s.Before != BeforeRestore).ToList();
            var taken = snapshots.FirstOrDefault(s => s.Is(justTaken));
            if (taken == null)
            {
                return [];
            }

            var earlierUpgrades = Newest(snapshots
                    .Where(s => s.Before != taken.Before)
                    .GroupBy(s => s.Before)
                    .Select(upgrade => Newest(upgrade).First()))
                .Take(UpgradeSnapshotsKept - 1);
            var kept = earlierUpgrades.Append(taken).ToHashSet();

            return Remove(snapshots.Where(s => !kept.Contains(s)), logger);
        });
    }

    /// <summary>Removes every snapshot of a user's database, when the user is deleted.</summary>
    /// <returns>The removed files.</returns>
    public static IReadOnlyList<string> RemoveAll(string snapshotFolder, Guid userId, ILogger logger)
    {
        return Housekeeping(logger, () => Remove(Snapshots(snapshotFolder, userId.ToString("D")).ToList(), logger));
    }

    private static IReadOnlyList<string> Housekeeping(ILogger logger, Func<IReadOnlyList<string>> prune)
    {
        try
        {
            return prune();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not remove old snapshots; they go with a later snapshot");

            return [];
        }
    }

    private static IEnumerable<Snapshot> Snapshots(string snapshotFolder, string databaseName)
    {
        if (!Directory.Exists(snapshotFolder))
        {
            return [];
        }

        var pattern = new Regex($"^{Regex.Escape(databaseName)}-(\\d{{8}}-\\d{{6}}(?:-\\d{{3}})?)-before-(.+)\\.db$", RegexOptions.CultureInvariant);

        return Directory.EnumerateFiles(snapshotFolder)
            .Select(path => (Path: path, Match: pattern.Match(Path.GetFileName(path))))
            .Where(file => file.Match.Success)
            .Select(file => new Snapshot(file.Path, file.Match.Groups[1].Value, file.Match.Groups[2].Value));
    }

    private static IEnumerable<Snapshot> Newest(IEnumerable<Snapshot> snapshots)
    {
        return snapshots
            .OrderByDescending(s => s.Time, StringComparer.Ordinal)
            .ThenByDescending(s => s.Path, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> Remove(IEnumerable<Snapshot> snapshots, ILogger logger)
    {
        var removed = new List<string>();
        foreach (var snapshot in snapshots)
        {
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

    private sealed record Snapshot(string Path, string Time, string Before)
    {
        public bool Is(string path)
        {
            return string.Equals(System.IO.Path.GetFileName(Path), System.IO.Path.GetFileName(path), StringComparison.Ordinal);
        }
    }
}
