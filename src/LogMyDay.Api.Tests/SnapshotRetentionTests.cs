using LogMyDay.Api.Infrastructure.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

/// <summary>
/// The snapshot folder keeps each user's newest snapshots before a restore and each database's
/// snapshots of its last upgrades. A deleted user's snapshots go with the user. Files that are
/// not LogMyDay's snapshots are never touched.
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
        var alices = Restores(Alice, "20261101-100000", "20261103-100000", "20261102-100000", "20261105-100000", "20261104-100000");
        var others = Others();

        var removed = SnapshotRetention.PruneRestoreSnapshots(_folder, Alice, NullLogger.Instance);

        // Kept: 11-05, 11-04 and 11-03, whatever order they were written in.
        AssertSame([alices[0], alices[2]], removed.Select(Path.GetFileName)!);
        AssertSame([.. others, alices[3], alices[4], alices[1]], Files());
    }

    [Fact]
    public void AfterAnUpgrade_TheDatabaseKeepsTheSnapshotsOfItsLastTwoUpgrades()
    {
        // The newest upgrade is not the one whose migration sorts last.
        var platform = new[]
        {
            Create("logmyday-20261101-100000-before-20261120000000_C.db"),
            Create("logmyday-20261102-100000-before-20261110000000_B.db"),
            Create("logmyday-20261103-100000-before-20261101000000_A.db"),
        };
        var others = Others();

        var removed = SnapshotRetention.PruneUpgradeSnapshots(_folder, "logmyday", NullLogger.Instance);

        AssertSame([platform[0]], removed.Select(Path.GetFileName)!);
        AssertSame([.. others, platform[1], platform[2]], Files());
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

        Assert.Empty(SnapshotRetention.PruneRestoreSnapshots(_folder, Alice, NullLogger.Instance));
        Assert.Empty(SnapshotRetention.PruneUpgradeSnapshots(_folder, Alice.ToString("D"), NullLogger.Instance));
        AssertSame([.. restores, .. upgrades], Files());
    }

    [Fact]
    public void ADeletedUser_LosesEverySnapshot_AndNoOtherFileGoes()
    {
        Restores(Alice, "20261101-100000", "20261102-100000");
        Create($"{Alice:D}-20261103-100000-before-20261120000000_A.db");
        var others = Others();

        SnapshotRetention.RemoveAll(_folder, Alice, NullLogger.Instance);

        AssertSame(others, Files());
    }

    [Fact]
    public void WithoutASnapshotFolder_ThereIsNothingToRemove()
    {
        var missing = Path.Combine(_folder, "no-snapshots-yet");

        Assert.Empty(SnapshotRetention.PruneRestoreSnapshots(missing, Alice, NullLogger.Instance));
        Assert.Empty(SnapshotRetention.PruneUpgradeSnapshots(missing, "logmyday", NullLogger.Instance));
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
        return Directory.GetFiles(_folder).Select(Path.GetFileName).ToArray()!;
    }

    private static void AssertSame(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }
}
