using System.IO.Compression;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Options;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Backup and restore of the database files: a user's database as one file, and the whole
/// installation as an archive that LogMyDay runs on once it is unpacked.
/// </summary>
public sealed class DatabaseBackupsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"lmd-backup-test-{Guid.NewGuid():N}");
    private readonly ServiceProvider _services;
    private Guid _admin;
    private Guid _friend;

    public DatabaseBackupsTests()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "data"));
        _services = BuildServices(Path.Combine(_folder, "data"));
    }

    private UserDatabases Databases => _services.GetRequiredService<UserDatabases>();

    private IDatabaseBackups Backups => _services.GetRequiredService<IDatabaseBackups>();

    public void Dispose()
    {
        _services.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task AUsersBackup_RestoresIntoTheirDatabase_AndTheReplacedStateIsKept()
    {
        await Start();
        await AddTag(_services, _friend, "Coffee");

        var backup = await Save(await Backups.ExportUser(_friend, CancellationToken.None));

        await AddTag(_services, _friend, "Logged after the backup");
        await AddTag(_services, _admin, "Admin's tag");

        await using (var content = File.OpenRead(backup))
        {
            await Backups.RestoreUser(_friend, content, CancellationToken.None);
        }

        Assert.Equal(["Coffee"], await TagNames(_services, _friend));
        Assert.Equal(["Admin's tag"], await TagNames(_services, _admin));

        // What the restore replaced is in the snapshot folder, and nothing is left of the upload.
        var snapshot = Assert.Single(Directory.GetFiles(Databases.SnapshotFolder, $"{_friend:D}-*-before-restore.db"));
        Assert.Equal(2L, Scalar(snapshot, "SELECT COUNT(*) FROM LogMyDay_Tags;"));
        Assert.Empty(Directory.GetFiles(Path.Combine(Databases.Root, DatabaseBackups.WorkFolderName)));

        // The restored database is in use again.
        await AddTag(_services, _friend, "Tea");
        Assert.Equal(["Coffee", "Tea"], await TagNames(_services, _friend));
    }

    [Fact]
    public async Task ARestore_WritesItsSnapshot_AndTheUserKeepsTheirThreeNewest()
    {
        await Start();
        await AddTag(_services, _friend, "Coffee");
        var backup = await Save(await Backups.ExportUser(_friend, CancellationToken.None));

        Directory.CreateDirectory(Databases.SnapshotFolder);
        var friends = new[] { "20250101-000000", "20250201-000000", "20250301-000000" }
            .Select(time => $"{_friend:D}-{time}-before-restore.db")
            .ToArray();
        var admins = $"{_admin:D}-20250101-000000-before-restore.db";
        foreach (var name in friends.Append(admins))
        {
            await File.WriteAllTextAsync(Path.Combine(Databases.SnapshotFolder, name), "an earlier state");
        }

        await using (var content = File.OpenRead(backup))
        {
            await Backups.RestoreUser(_friend, content, CancellationToken.None);
        }

        var kept = Directory.GetFiles(Databases.SnapshotFolder, $"{_friend:D}-*-before-restore.db").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(3, kept.Length);
        Assert.Equal([friends[1], friends[2]], kept[..2]);
        Assert.Equal(1L, Scalar(Path.Combine(Databases.SnapshotFolder, kept[2]!), "SELECT COUNT(*) FROM LogMyDay_Tags;"));
        Assert.True(File.Exists(Path.Combine(Databases.SnapshotFolder, admins)));
    }

    [Fact]
    public async Task ABackupOfAnotherUser_IsRefused_AndNothingChanges()
    {
        await Start();
        await AddTag(_services, _friend, "Friend's tag");
        await AddTag(_services, _admin, "Admin's tag");
        var backup = await Save(await Backups.ExportUser(_friend, CancellationToken.None));

        await using var content = File.OpenRead(backup);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Backups.RestoreUser(_admin, content, CancellationToken.None));

        Assert.Contains("another user's data", error.Message);
        Assert.Equal(["Admin's tag"], await TagNames(_services, _admin));
        Assert.False(Directory.Exists(Databases.SnapshotFolder));
    }

    [Fact]
    public async Task AFileThatIsNotAUsersDatabase_IsRefused()
    {
        await Start();
        await AddTag(_services, _friend, "Keep");

        var notADatabase = Path.Combine(_folder, "notes.db");
        await File.WriteAllTextAsync(notADatabase, new string('x', 5000));
        var platformCopy = Path.Combine(_folder, "platform-copy.db");
        UserDatabases.WriteCopy(Databases.PlatformConnectionString, platformCopy);

        Assert.Contains("not a SQLite database", (await Refused(notADatabase)).Message);
        Assert.Contains("user accounts", (await Refused(platformCopy)).Message);
        Assert.Equal(["Keep"], await TagNames(_services, _friend));
    }

    [Fact]
    public async Task ABackupFromANewerVersion_IsRefused()
    {
        await Start();
        await AddTag(_services, _friend, "Keep");
        var backup = await Save(await Backups.ExportUser(_friend, CancellationToken.None));
        Execute(backup, "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29990101000000_FromTheFuture', '9.0.0');");

        var error = await Refused(backup);

        Assert.Contains("newer LogMyDay", error.Message);
        Assert.Contains("29990101000000_FromTheFuture", error.Message);
        Assert.Equal(["Keep"], await TagNames(_services, _friend));
    }

    [Fact]
    public async Task TheInstallationArchive_HoldsEveryDatabase_AndLogMyDayRunsOnItUnpacked()
    {
        await Start();
        await AddTag(_services, _friend, "Friend's tag");
        await AddTag(_services, _admin, "Admin's tag");

        var archive = await Save(await Backups.ExportInstallation(CancellationToken.None));

        var restored = Path.Combine(_folder, "restored");
        ZipFile.ExtractToDirectory(archive, restored);
        Assert.Equal(
            new[] { "logmyday.db", $"users/{_admin:D}.db", $"users/{_friend:D}.db" }.Order(),
            Directory.GetFiles(restored, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(restored, f).Replace('\\', '/')).Order());

        var hasher = new Mock<IPasswordHasher>();
        await using var other = BuildServices(restored, hasher.Object);
        await using (var scope = other.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>().SeedAsync();

            var users = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.Select(u => u.Id).ToListAsync();
            Assert.Equal(new[] { _admin, _friend }.Order(), users.Order());
        }

        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never, "No admin is seeded: the accounts came with the archive.");
        Assert.Equal(["Friend's tag"], await TagNames(other, _friend));
        Assert.Equal(["Admin's tag"], await TagNames(other, _admin));
        Assert.False(Directory.Exists(Path.Combine(restored, DatabaseUpgrade.SnapshotFolderName)), "Nothing had to be migrated.");
    }

    [Fact]
    public async Task AnAdmin_ClearsOneUsersData_OrEveryUsers_EachInItsOwnDatabase()
    {
        await Start();
        await AddTag(_services, _friend, "Friend's tag");
        await AddTag(_services, _admin, "Admin's tag");

        await using (var scope = _services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserDataAdminService>().ClearData(_friend, CancellationToken.None);
        }

        Assert.Empty(await TagNames(_services, _friend));
        Assert.Equal(["Admin's tag"], await TagNames(_services, _admin));

        await AddTag(_services, _friend, "Friend's new tag");
        await using (var scope = _services.CreateAsyncScope())
        {
            var admin = scope.ServiceProvider.GetRequiredService<IUserDataAdminService>();
            Assert.Equal(["Friend's new tag"], (await admin.ExportData(_friend, CancellationToken.None)).Tags.Select(t => t.TagName));

            Assert.Equal(2, await admin.ClearData(null, CancellationToken.None));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => admin.ClearData(Guid.NewGuid(), CancellationToken.None));
        }

        Assert.Empty(await TagNames(_services, _friend));
        Assert.Empty(await TagNames(_services, _admin));

        // The reference data stays, so the databases still work.
        await using var friend = _services.GetRequiredService<IUserScopeFactory>().Create(_friend);
        var data = friend.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        Assert.True(await data.InputTypes.AnyAsync());
        Assert.True(await data.Units.AnyAsync());
    }

    private static ServiceProvider BuildServices(string dataFolder, IPasswordHasher? passwordHasher = null)
    {
        if (passwordHasher == null)
        {
            var hasher = new Mock<IPasswordHasher>();
            hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash");
            passwordHasher = hasher.Object;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogMyDayDatabases($"Data Source={Path.Combine(dataFolder, "logmyday.db")}");
        services.AddSingleton(passwordHasher);
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IReminderDayBackfill, ReminderDayBackfill>();
        services.AddScoped<IReminderRecurrenceMigration, ReminderRecurrenceMigration>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        services.Configure<InitialAdminOptions>(o =>
        {
            o.Email = "admin@example.com";
            o.InitialPassword = "first-admin-password";
        });
        services.AddSingleton(new Mock<IEmailSender>().Object);
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITagRuleEngine, TagRuleEngine>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IUserDataAdminService, UserDataAdminService>();

        return services.BuildServiceProvider();
    }

    /// <summary>Starts the installation: the seeded admin, and a friend the admin adds.</summary>
    private async Task Start()
    {
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>().SeedAsync();
        _admin = (await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.SingleAsync()).Id;

        var users = scope.ServiceProvider.GetRequiredService<IUserService>();
        _friend = (await users.CreateUser("friend@example.com", "Friend-Pa55word!", "Friend", false, "en-US", "Europe/Prague", _admin, CancellationToken.None)).Id;
    }

    private static async Task AddTag(ServiceProvider services, Guid userId, string name)
    {
        await using var scope = services.GetRequiredService<IUserScopeFactory>().Create(userId);
        var data = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        data.Tags.Add(new Tag { TagName = name, UserId = userId });
        await data.SaveChangesAsync();
    }

    private static async Task<List<string>> TagNames(ServiceProvider services, Guid userId)
    {
        await using var scope = services.GetRequiredService<IUserScopeFactory>().Create(userId);
        var data = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();

        return await data.Tags.Select(t => t.TagName).OrderBy(n => n).ToListAsync();
    }

    /// <summary>Saves a download the way a browser would; disposing it deletes its temporary file.</summary>
    private async Task<string> Save(DatabaseDownload download)
    {
        var path = Path.Combine(_folder, $"{Guid.NewGuid():N}-{download.FileName}");
        string temporaryFile;
        await using (download)
        {
            temporaryFile = ((FileStream)download.Content).Name;
            await using var file = File.Create(path);
            await download.Content.CopyToAsync(file);
        }

        Assert.False(File.Exists(temporaryFile), "Disposing the download deletes its temporary file.");

        return path;
    }

    private async Task<InvalidDataException> Refused(string path)
    {
        await using var content = File.OpenRead(path);

        return await Assert.ThrowsAsync<InvalidDataException>(() => Backups.RestoreUser(_friend, content, CancellationToken.None));
    }

    private static void Execute(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Scalar(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (long)command.ExecuteScalar()!;
    }
}
