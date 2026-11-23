namespace LogMyDay.Api.Infrastructure.Data;

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Names and writes the snapshots, and decides which stay in the snapshot folder. A snapshot is
/// named <c>&lt;database&gt;-&lt;time&gt;-before-&lt;what&gt;.db</c>: before a restore of a user's
/// database (<c>before-restore</c>), or before an upgrade (<c>before-&lt;the upgrade's last
/// migration&gt;</c>). Files with other names are never touched. Removing snapshots is
/// housekeeping: it never fails the operation that wrote one.
/// </summary>
public static class SnapshotRetention
{
    /// <summary>The snapshots before a restore that each user keeps.</summary>
    public const int RestoreSnapshotsKept = 3;

    /// <summary>The upgrades whose snapshots each database keeps.</summary>
    public const int UpgradesKept = 2;

    /// <summary>What a snapshot before a restore comes before.</summary>
    public const string BeforeRestore = "restore";

    /// <summary>
    /// The time in a new snapshot's name, in UTC. Earlier names have it to the second or to the
    /// millisecond; they still count.
    /// </summary>
    private const string TimeFormat = "yyyyMMdd-HHmmss-fffffff";

    /// <summary>
    /// The path of a new snapshot of a database, taken now. Never a file that exists: VACUUM INTO
    /// refuses to write over one.
    /// </summary>
    /// <param name="databaseName">The database file's name without its extension.</param>
    /// <param name="before">What the snapshot comes before: <see cref="BeforeRestore"/> or the upgrade's last migration.</param>
    /// <param name="utcNow">The time to name it by; now when not given.</param>
    public static string NewPath(string snapshotFolder, string databaseName, string before, DateTime? utcNow = null)
    {
        var time = utcNow ?? DateTime.UtcNow;
        while (true)
        {
            var path = Path.Combine(snapshotFolder, $"{databaseName}-{time.ToString(TimeFormat, CultureInfo.InvariantCulture)}-before-{before}.db");
            if (!File.Exists(path))
            {
                return path;
            }

            time = time.AddTicks(1);
        }
    }

    /// <summary>Writes a snapshot. A write that fails, e.g. on a full disk, leaves no file behind.</summary>
    public static void Write(string snapshot, Action write)
    {
        // The catch is needed: a partial file would count as a snapshot and push out a real one.
        try
        {
            write();
        }
        catch
        {
            TryDelete(snapshot);

            throw;
        }
    }

    /// <summary>
    /// After a successful restore: the user keeps the snapshot just taken and their newest
    /// others, up to <see cref="RestoreSnapshotsKept"/>.
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
    /// After a migration, whether it succeeded or not: the database keeps a snapshot of each of
    /// its last <see cref="UpgradesKept"/> upgrades, the first one taken, which shows the state
    /// before the upgrade began. The snapshot just taken also stays. An upgrade that is tried
    /// again names its snapshots after the same last migration, so its attempts count as one
    /// upgrade, and a start that keeps failing keeps two copies instead of adding one each time.
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

            var upgrades = snapshots.GroupBy(s => s.Before).ToList();
            var current = upgrades.Single(upgrade => upgrade.Key == taken.Before);
            var earlier = upgrades
                .Where(upgrade => upgrade != current)
                .OrderByDescending(upgrade => Newest(upgrade).First().Time, StringComparer.Ordinal)
                .Take(UpgradesKept - 1);
            var kept = earlier.Append(current).Select(upgrade => Newest(upgrade).Last()).Append(taken).ToHashSet();

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
        // The catch is needed: the restore, migration or delete that calls this has already
        // happened, and a folder that can't be listed now must not report it as failed.
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

        var pattern = new Regex($"^{Regex.Escape(databaseName)}-(\\d{{8}}-\\d{{6}}(?:-\\d{{3,7}})?)-before-(.+)\\.db$", RegexOptions.CultureInvariant);

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
            if (TryDelete(snapshot.Path, logger))
            {
                removed.Add(snapshot.Path);
            }
        }

        if (removed.Count > 0)
        {
            logger.LogInformation("Removed snapshots: {Snapshots}", string.Join(", ", removed.Select(Path.GetFileName)));
        }

        return removed;
    }

    private static bool TryDelete(string path, ILogger? logger = null)
    {
        // The catch is needed: one file that can't be removed now goes with a later snapshot, and
        // must neither stop the others nor fail the caller.
        try
        {
            File.Delete(path);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Could not remove the snapshot {Snapshot}", path);

            return false;
        }
    }

    private sealed record Snapshot(string Path, string Time, string Before)
    {
        public bool Is(string path)
        {
            return string.Equals(System.IO.Path.GetFileName(Path), System.IO.Path.GetFileName(path), StringComparison.Ordinal);
        }
    }
}
