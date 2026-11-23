using LogMyDay.Api.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Every database is built and upgraded by its context's migrations. A database that already
/// holds data is copied aside before a migration touches it, and a failure names the file.
/// </summary>
public sealed class DatabaseUpgradeTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"lmd-upgrade-test-{Guid.NewGuid():N}");

    public DatabaseUpgradeTests()
    {
        Directory.CreateDirectory(_folder);
    }

    private string DatabasePath => Path.Combine(_folder, "test.db");

    private string SnapshotFolder => Path.Combine(_folder, DatabaseUpgrade.SnapshotFolderName);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void TheMigrations_BuildANewUserDatabase_ThatMatchesTheModel()
    {
        using var db = new LogMyDayDbContext(Options<LogMyDayDbContext>());

        Assert.Null(DatabaseUpgrade.Migrate(db, SnapshotFolder, NullLogger.Instance));

        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.True(db.InputTypes.Any());
        Assert.False(Directory.Exists(SnapshotFolder));
    }

    [Fact]
    public void TheMigrations_BuildANewPlatformDatabase_ThatMatchesTheModel()
    {
        using var db = new PlatformDbContext(Options<PlatformDbContext>());

        Assert.Null(DatabaseUpgrade.Migrate(db, SnapshotFolder, NullLogger.Instance));

        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(db.Users);
    }

    [Fact]
    public void AnUpToDateDatabase_IsLeftAlone()
    {
        using var db = new LogMyDayDbContext(Options<LogMyDayDbContext>());
        DatabaseUpgrade.Migrate(db, SnapshotFolder, NullLogger.Instance);

        Assert.Null(DatabaseUpgrade.Migrate(db, SnapshotFolder, NullLogger.Instance));
        Assert.False(Directory.Exists(SnapshotFolder));
    }

    [Fact]
    public void ADatabaseWithData_IsCopiedAside_BeforeItIsMigrated()
    {
        using var db = new TwoStepContext(Options<TwoStepContext>());
        db.GetService<IMigrator>().Migrate(TwoStepContext.First);
        db.Database.ExecuteSqlRaw("INSERT INTO Notes (Text) VALUES ('kept');");

        var snapshot = DatabaseUpgrade.Migrate(db, SnapshotFolder, NullLogger.Instance);

        Assert.NotNull(snapshot);
        Assert.StartsWith(SnapshotFolder, snapshot);
        Assert.Contains(TwoStepContext.Second, Path.GetFileName(snapshot));
        Assert.Empty(db.Database.GetPendingMigrations());

        // The snapshot is the database as it was: the row is there, the second migration is not.
        using var before = new TwoStepContext(new DbContextOptionsBuilder<TwoStepContext>().UseSqlite($"Data Source={snapshot};Pooling=False").Options);
        Assert.Equal([TwoStepContext.First], before.Database.GetAppliedMigrations());
        Assert.Equal(1, before.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Notes").AsEnumerable().Single());
    }

    [Fact]
    public void AFailingMigration_NamesTheDatabase_AndTheSnapshotExists()
    {
        using var db = new BrokenStepContext(Options<BrokenStepContext>());
        db.GetService<IMigrator>().Migrate(BrokenStepContext.First);

        var error = Assert.Throws<InvalidOperationException>(() => DatabaseUpgrade.Migrate(db, SnapshotFolder, NullLogger.Instance));

        Assert.Contains(DatabasePath, error.Message);
        var snapshot = Assert.Single(Directory.GetFiles(SnapshotFolder));
        Assert.Contains(snapshot, error.Message);
    }

    [Fact]
    public void AnUpgrade_KeepsItsSnapshotAndThePreviousUpgrades_NotTheSnapshotOfAFailedAttempt()
    {
        var previousUpgrade = OldSnapshots("test-20250101-000000-before-20200101000000_First.db");
        var failedAttempt = OldSnapshots($"test-20250601-000000-before-{TwoStepContext.Second}.db");
        var others = OldSnapshots("test-20240101-000000-before-20190101000000_Older.db", "other-20250101-000000-before-20200101000000_First.db", "test-20250101-000000-before-restore.db");
        using var db = new TwoStepContext(Options<TwoStepContext>());
        db.GetService<IMigrator>().Migrate(TwoStepContext.First);
        db.Database.ExecuteSqlRaw("INSERT INTO Notes (Text) VALUES ('kept');");

        var snapshot = DatabaseUpgrade.Migrate(db, SnapshotFolder, NullLogger.Instance);

        // Kept: this upgrade's snapshot, which replaces the failed attempt's, and the previous
        // upgrade's. The upgrade before that goes; other databases' files stay.
        string[] expected = [Path.GetFileName(snapshot)!, .. previousUpgrade, others[1], others[2]];
        Assert.Equal(expected.Order(StringComparer.Ordinal), SnapshotFiles());
        Assert.DoesNotContain(failedAttempt[0], SnapshotFiles());
    }

    [Fact]
    public void AStartThatFailsAgainAndAgain_KeepsOneSnapshotOfTheFailingMigration()
    {
        var previousUpgrade = OldSnapshots("test-20250101-000000-before-20200101000000_First.db");
        using var db = new BrokenStepContext(Options<BrokenStepContext>());
        db.GetService<IMigrator>().Migrate(BrokenStepContext.First);

        InvalidOperationException? error = null;
        for (var start = 0; start < 3; start++)
        {
            error = Assert.Throws<InvalidOperationException>(() => DatabaseUpgrade.Migrate(db, SnapshotFolder, NullLogger.Instance));
        }

        var snapshot = Assert.Single(Directory.GetFiles(SnapshotFolder, "*-before-20200102000000_Broken.db"));
        Assert.Contains(snapshot, error!.Message);
        string[] expected = [Path.GetFileName(snapshot), .. previousUpgrade];
        Assert.Equal(expected.Order(StringComparer.Ordinal), SnapshotFiles());
    }

    [Fact]
    public void AFileOfAnotherOrigin_IsRefused_AndLeftAsItIs()
    {
        using var db = new LogMyDayDbContext(Options<LogMyDayDbContext>());
        db.Database.ExecuteSqlRaw("CREATE TABLE LogMyDay_Tags (Id INTEGER PRIMARY KEY);");

        var error = Assert.Throws<InvalidOperationException>(() => DatabaseUpgrade.Migrate(db, SnapshotFolder, NullLogger.Instance));

        Assert.Contains(DatabasePath, error.Message);
        Assert.Contains("older LogMyDay", error.Message);
        Assert.Equal(0, db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE name = 'LogMyDay_Activities'").AsEnumerable().Single());
    }

    private string[] SnapshotFiles()
    {
        return Directory.GetFiles(SnapshotFolder).Select(path => Path.GetFileName(path)!).Order(StringComparer.Ordinal).ToArray();
    }

    private string[] OldSnapshots(params string[] names)
    {
        Directory.CreateDirectory(SnapshotFolder);
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(SnapshotFolder, name), "an earlier state");
        }

        return names;
    }

    private DbContextOptions<TContext> Options<TContext>()
        where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>().UseSqlite($"Data Source={DatabasePath}").Options;
    }
}

/// <summary>A context with two migrations, to upgrade a database that already has the first.</summary>
public sealed class TwoStepContext(DbContextOptions<TwoStepContext> options) : DbContext(options)
{
    public const string First = "20200101000000_First";
    public const string Second = "20200102000000_Second";

    [DbContext(typeof(TwoStepContext))]
    [Migration(First)]
    private sealed class FirstMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TABLE Notes (Id INTEGER PRIMARY KEY, Text TEXT);");
        }
    }

    [DbContext(typeof(TwoStepContext))]
    [Migration(Second)]
    private sealed class SecondMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE Notes ADD COLUMN Done INTEGER NOT NULL DEFAULT 0;");
        }
    }
}

/// <summary>A context whose second migration fails.</summary>
public sealed class BrokenStepContext(DbContextOptions<BrokenStepContext> options) : DbContext(options)
{
    public const string First = "20200101000000_First";

    [DbContext(typeof(BrokenStepContext))]
    [Migration(First)]
    private sealed class FirstMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE TABLE Notes (Id INTEGER PRIMARY KEY, Text TEXT);");
        }
    }

    [DbContext(typeof(BrokenStepContext))]
    [Migration("20200102000000_Broken")]
    private sealed class BrokenMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE NoSuchTable ADD COLUMN Done INTEGER;");
        }
    }
}
