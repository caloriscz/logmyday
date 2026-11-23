using LogMyDay.Api.Infrastructure.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

/// <summary>
/// The snapshot folder keeps each user's newest snapshots before a restore and each database's
/// snapshots of its last upgrades. The snapshot just taken always stays. A deleted user's
/// snapshots go with the user. Files that are not LogMyDay's snapshots are never touched.
/// </summary>
public sealed class SnapshotRetentionTests : IDisposable
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"lmd-snapshots-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void AfterARestore_TheUserKeepsTheirThreeNewestRestoreSnapshots()
    {
        var alices = Restores(Alice, "20261101-100000", "20261103-100000", "20261102-100000", "20261104-100000", "20261105-100000-250");
        var others = Others();

        var removed = SnapshotRetention.PruneRestoreSnapshots(_folder, Alice, alices[4], NullLogger.Instance);

        // Kept: 11-05 (just taken), 11-04 and 11-03, whatever order they were written in.
        AssertSame([alices[0], alices[2]], Names(removed));
        AssertSame([.. others, alices[4], alices[3], alices[1]], Files());
    }

    [Fact]
    public void TheSnapshotJustTaken_Stays_EvenWhenTheClockWentBack()
    {
        var ahead = Restores(Alice, "20261210-100000", "20261211-100000", "20261212-100000");
        var justTaken = Create($"{Alice:D}-20261123-100000-000-before-restore.db");

        var removed = SnapshotRetention.PruneRestoreSnapshots(_folder, Alice, Path.Combine(_folder, justTaken), NullLogger.Instance);

        AssertSame([ahead[0]], Names(removed));
        AssertSame([justTaken, ahead[1], ahead[2]], Files());
    }

    [Fact]
    public void AfterAnUpgrade_TheDatabaseKeepsTheSnapshotsOfItsLastTwoUpgrades()
    {
        // The newest upgrade is not the one whose migration sorts last.
        var platform = new[]
        {
            Create("logmyday-20261101-100000-before-20261120000000_C.db"),
            Create("logmyday-20261102-100000-before-20261110000000_B.db"),
            Create("logmyday-20261103-100000-000-before-20261101000000_A.db"),
        };
        var others = Others();

        var removed = SnapshotRetention.PruneUpgradeSnapshots(_folder, "logmyday", platform[2], NullLogger.Instance);

        AssertSame([platform[0]], Names(removed));
        AssertSame([.. others, platform[1], platform[2]], Files());
    }

    [Fact]
    public void RetriesOfAnUpgrade_KeepItsFirstAndLatestSnapshot_AndThePreviousUpgradeStays()
    {
        var previousUpgrade = Create("logmyday-20261101-100000-before-20261101000000_W.db");
        var firstAttempt = Create("logmyday-20261102-100000-000-before-20261120000000_X.db");
        var secondAttempt = Create("logmyday-20261102-100100-000-before-20261120000000_X.db");
        var retry = Create("logmyday-20261103-100000-0000000-before-20261120000000_X.db");

        var removed = SnapshotRetention.PruneUpgradeSnapshots(_folder, "logmyday", retry, NullLogger.Instance);

        // The first attempt shows the state before the upgrade began, whatever a failed attempt
        // may have left behind.
        AssertSame([secondAttempt], Names(removed));
        AssertSame([previousUpgrade, firstAttempt, retry], Files());
    }

    [Fact]
    public void OfEachEarlierUpgrade_TheFirstSnapshotStays()
    {
        var olderUpgrade = Create("logmyday-20261001-100000-before-20261001000000_V.db");
        var previousFirst = Create("logmyday-20261101-100000-before-20261101000000_W.db");
        var previousRetry = Create("logmyday-20261101-100500-before-20261101000000_W.db");
        var justTaken = Create("logmyday-20261201-100000-0000000-before-20261201000000_X.db");

        var removed = SnapshotRetention.PruneUpgradeSnapshots(_folder, "logmyday", justTaken, NullLogger.Instance);

        AssertSame([olderUpgrade, previousRetry], Names(removed));
        AssertSame([previousFirst, justTaken], Files());
    }

    [Fact]
    public void AUsersUpgradeSnapshots_AndRestoreSnapshots_AreKeptApart()
    {
        var restores = Restores(Alice, "20261101-100000", "20261102-100000", "20261103-100000");
        var upgrades = new[]
        {
            Create($"{Alice:D}-20261104-100000-before-20261120000000_A.db"),
            Create($"{Alice:D}-20261105-100000-before-20261121000000_B.db"),
        };

        Assert.Empty(SnapshotRetention.PruneRestoreSnapshots(_folder, Alice, restores[2], NullLogger.Instance));
        Assert.Empty(SnapshotRetention.PruneUpgradeSnapshots(_folder, Alice.ToString("D"), upgrades[1], NullLogger.Instance));
        AssertSame([.. restores, .. upgrades], Files());
    }

    [Fact]
    public void ANewSnapshotsName_IsOneThatTheRetentionCounts()
    {
        var older = Restores(Alice, "20201101-100000", "20201102-100000-500", "20201103-100000");
        var justTaken = Create(Path.GetFileName(SnapshotRetention.NewPath(_folder, Alice.ToString("D"), SnapshotRetention.BeforeRestore)));

        var removed = SnapshotRetention.PruneRestoreSnapshots(_folder, Alice, justTaken, NullLogger.Instance);

        AssertSame([older[0]], Names(removed));
    }

    [Fact]
    public void ANewSnapshot_NeverGetsTheNameOfAFileThatExists()
    {
        var now = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        var first = SnapshotRetention.NewPath(_folder, "logmyday", "20261120000000_X", now);
        File.WriteAllText(first, "taken");

        var second = SnapshotRetention.NewPath(_folder, "logmyday", "20261120000000_X", now);

        Assert.NotEqual(first, second);
        Assert.False(File.Exists(second));
    }

    [Fact]
    public void ASnapshotThatFailsWhileBeingWritten_LeavesNoFile()
    {
        var snapshot = SnapshotRetention.NewPath(_folder, Alice.ToString("D"), SnapshotRetention.BeforeRestore);

        var error = Assert.Throws<IOException>(() => SnapshotRetention.Write(snapshot, () =>
        {
            File.WriteAllText(snapshot, "the first half");

            throw new IOException("The disk is full.");
        }));

        Assert.Equal("The disk is full.", error.Message);
        Assert.False(File.Exists(snapshot));
    }

    [Fact]
    public void WithoutTheSnapshotJustTaken_NothingIsRemoved()
    {
        var restores = Restores(Alice, "20261101-100000", "20261102-100000", "20261103-100000", "20261104-100000");

        Assert.Empty(SnapshotRetention.PruneRestoreSnapshots(_folder, Alice, "missing.db", NullLogger.Instance));
        AssertSame(restores, Files());
    }

    [Fact]
    public void ADeletedUser_LosesEverySnapshot_AndNoOtherFileGoes()
    {
        Restores(Alice, "20261101-100000", "20261102-100000-500");
        Create($"{Alice:D}-20261103-100000-before-20261120000000_A.db");
        var others = Others();

        SnapshotRetention.RemoveAll(_folder, Alice, NullLogger.Instance);

        AssertSame(others, Files());
    }

    [Fact]
    public void WithoutASnapshotFolder_ThereIsNothingToRemove()
    {
        var missing = Path.Combine(_folder, "no-snapshots-yet");

        Assert.Empty(SnapshotRetention.PruneRestoreSnapshots(missing, Alice, "a.db", NullLogger.Instance));
        Assert.Empty(SnapshotRetention.PruneUpgradeSnapshots(missing, "logmyday", "a.db", NullLogger.Instance));
        Assert.Empty(SnapshotRetention.RemoveAll(missing, Alice, NullLogger.Instance));
    }

    /// <summary>Snapshots of other users and databases, and files that are no snapshots at all.</summary>
    private string[] Others()
    {
        return
        [
            Create($"{Bob:D}-20261101-090000-before-restore.db"),
            Create($"{Bob:D}-20261102-090000-before-restore.db"),
            Create("archive-20261101-090000-before-20261120000000_Initial.db"),
            Create($"{Alice:D}-copy.db"),
            Create("notes.txt"),
        ];
    }

    private string[] Restores(Guid userId, params string[] times)
    {
        return times.Select(time => Create($"{userId:D}-{time}-before-restore.db")).ToArray();
    }

    private string Create(string name)
    {
        File.WriteAllText(Path.Combine(_folder, name), name);

        return name;
    }

    private string[] Files()
    {
        return Names(Directory.GetFiles(_folder));
    }

    private static string[] Names(IEnumerable<string> paths)
    {
        return paths.Select(path => Path.GetFileName(path)!).ToArray();
    }

    private static void AssertSame(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }
}
