using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// An installation of v0.7.2 keeps everything in one file. The first start of this version turns
/// it into the platform database and one database per user, and keeps the original as a backup.
/// </summary>
public sealed class SingleFileSplitTests : IDisposable
{
    private static readonly Guid Admin = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Nobody = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"lmd-split-test-{Guid.NewGuid():N}");

    public SingleFileSplitTests()
    {
        Directory.CreateDirectory(_folder);
    }

    private string DatabasePath => Path.Combine(_folder, "logmyday.db");

    private string BackupPath => Path.Combine(_folder, "logmyday.before-v0.8.0.db");

    private string UsersFolder => Path.Combine(_folder, "users");

    private string ConnectionString => $"Data Source={DatabasePath}";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Split_GivesEveryUserTheirRows_AndKeepsTheOriginalAsABackup()
    {
        await SeedSingleFile();
        var sourceRows = Tables(DatabasePath).ToDictionary(t => t, t => Rows(DatabasePath, t));

        var report = Split().SplitIfNeeded();

        Assert.NotNull(report);
        Assert.Equal(BackupPath, report.BackupPath);
        Assert.Equal([Admin, Alice, Bob], report.Users.Order());
        Assert.False(Directory.Exists(Path.Combine(_folder, "split-work")));

        // The backup is the original, row for row.
        foreach (var (table, rows) in sourceRows)
        {
            Assert.Equal(rows, Rows(BackupPath, table));
        }

        // The platform database has the accounts and the installation's settings, and no user data.
        Assert.Equal(["LogMyDay_ApiKeys", "LogMyDay_PasswordResets", "LogMyDay_Settings", "LogMyDay_Users"], Tables(DatabasePath));
        Assert.Equal(sourceRows["LogMyDay_Users"], Rows(DatabasePath, "LogMyDay_Users"));
        Assert.Equal(sourceRows["LogMyDay_ApiKeys"], Rows(DatabasePath, "LogMyDay_ApiKeys"));
        Assert.Equal(sourceRows["LogMyDay_PasswordResets"], Rows(DatabasePath, "LogMyDay_PasswordResets"));
        Assert.Equal("Ai:Model", Assert.Single(Rows(DatabasePath, "LogMyDay_Settings"))["Key"]);

        var files = new[] { Admin, Alice, Bob }.ToDictionary(u => u, u => Path.Combine(UsersFolder, $"{u}.db"));
        Assert.Equal(files.Values.Order(), Directory.GetFiles(UsersFolder).Order());

        using var model = new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite("Data Source=:memory:").Options);
        foreach (var entityType in model.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName()!;
            var owner = DataOwnership.Map[entityType.ClrType].Owner;
            var isOptionList = table is "LogMyDay_TagOptionLists" or "LogMyDay_TagOptions";

            if (owner == DataOwner.Reference)
            {
                Assert.All(files.Values, file => Assert.Equal(sourceRows[table], Rows(file, table)));
            }
            else if (owner == DataOwner.PlatformAndUser)
            {
                // The flag of a one-time migration that already ran goes to every user.
                Assert.All(files.Values, file => Assert.Equal(ReminderDayBackfill.FlagKey, Assert.Single(Rows(file, table))["Key"]));
            }
            else if (!isOptionList)
            {
                // Every row is in exactly one user's database, unchanged; only rows of no user are left out.
                var expected = sourceRows[table].Where(r => !r.TryGetValue("UserId", out var u) || !Same(u, Nobody)).ToList();
                var split = files.Values.SelectMany(file => Rows(file, table)).OrderBy(Text).ToList();

                Assert.True(expected.Count > 0, $"The test seeds no rows into {table}.");
                Assert.Equal(expected.OrderBy(Text).Select(Text), split.Select(Text));

                if (expected[0].ContainsKey("UserId"))
                {
                    Assert.All(files, file => Assert.All(Rows(file.Value, table), r => Assert.True(Same(r["UserId"], file.Key))));
                }
            }
        }

        Assert.Equal(1, report.LeftBehindRows["LogMyDay_Activities"]);
        Assert.Equal(sourceRows["LogMyDay_Activities"].Count - 1, report.CopiedRows["LogMyDay_Activities"]);

        // Alice's own list stays hers. The shared list goes to Bob, who uses it, with its options;
        // the shared list nobody uses goes to the admin. Keys don't change, so the tags still point at them.
        Assert.Equal(["Alice's"], ListNames(files[Alice]));
        Assert.Equal(["Shared moods"], ListNames(files[Bob]));
        Assert.Equal(["Shared unused"], ListNames(files[Admin]));
        Assert.All(files, file => Assert.All(Rows(file.Value, "LogMyDay_TagOptionLists"), r => Assert.True(Same(r["UserId"], file.Key))));
        Assert.Equal(2, Rows(files[Bob], "LogMyDay_TagOptions").Count);
        var bobsMood = Rows(files[Bob], "LogMyDay_Tags").Single(t => Equals(t["TagName"], "Bob's mood"));
        Assert.Equal(Rows(files[Bob], "LogMyDay_TagOptionLists").Single()["Id"], bobsMood["OptionListId"]);
    }

    [Fact]
    public async Task Startup_SplitsTheSingleFile_AndRunsOnTheResult()
    {
        await SeedSingleFile();

        var hasher = new Mock<IPasswordHasher>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogMyDayDatabases(ConnectionString);
        services.AddSingleton(hasher.Object);
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IReminderDayBackfill, ReminderDayBackfill>();
        services.AddScoped<IReminderRecurrenceMigration, ReminderRecurrenceMigration>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        await using var provider = services.BuildServiceProvider();

        // Twice: the second start finds nothing left to split.
        for (var start = 0; start < 2; start++)
        {
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>().SeedAsync();
        }

        Assert.True(File.Exists(BackupPath));
        hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never, "No admin is seeded: the accounts came with the split.");

        await using var alice = provider.GetRequiredService<IUserScopeFactory>().Create(Alice);
        var data = alice.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        Assert.Equal(["Alice's tag", "Alice's total", "Alice's workout"], await data.Tags.Select(t => t.TagName).OrderBy(n => n).ToListAsync());
        Assert.Equal(1, await data.Activities.CountAsync(a => a.UserId == Alice));
        Assert.Empty(await data.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task ADatabaseOlderThanTheLastSingleFileVersion_IsRefused_AndLeftAsItIs()
    {
        await SeedSingleFile();
        Execute(DatabasePath, "DELETE FROM __EFMigrationsHistory;");

        var error = Assert.Throws<InvalidOperationException>(() => Split().SplitIfNeeded());

        Assert.Contains("v0.7.2", error.Message);
        AssertNothingChanged();
    }

    [Fact]
    public async Task AFailedSplit_LeavesTheOriginalInPlace_AndNoNewFiles()
    {
        await SeedSingleFile();
        Execute(DatabasePath, "DROP TABLE LogMyDay_TodoItems;");

        var error = Assert.Throws<InvalidOperationException>(() => Split().SplitIfNeeded());

        Assert.Contains(DatabasePath, error.Message);
        Assert.Contains("LogMyDay_TodoItems", error.Message);
        AssertNothingChanged();
    }

    [Fact]
    public async Task ADatabaseThatIsAlreadySplit_IsLeftAlone()
    {
        await using (var platform = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(ConnectionString).Options))
        {
            await platform.Database.MigrateAsync();
        }

        Assert.Null(Split().SplitIfNeeded());
        Assert.False(File.Exists(BackupPath));
    }

    [Fact]
    public void ANewInstallation_HasNothingToSplit()
    {
        Assert.Null(Split().SplitIfNeeded());
        Assert.False(File.Exists(DatabasePath));
    }

    [Fact]
    public void AnInterruptedSplit_StopsTheStart_InsteadOfCreatingAnEmptyInstallation()
    {
        File.WriteAllText(BackupPath, "the original");

        var error = Assert.Throws<InvalidOperationException>(() => Split().SplitIfNeeded());

        Assert.Contains(BackupPath, error.Message);
        Assert.False(File.Exists(DatabasePath));
    }

    private SingleFileSplit Split()
    {
        return new SingleFileSplit(ConnectionString, new UserDatabases(ConnectionString, NullLogger<UserDatabases>.Instance), NullLogger<SingleFileSplit>.Instance);
    }

    private void AssertNothingChanged()
    {
        Assert.True(File.Exists(DatabasePath));
        Assert.Contains("LogMyDay_Activities", Tables(DatabasePath));
        Assert.False(File.Exists(BackupPath));
        Assert.False(Directory.Exists(UsersFolder));
        Assert.False(Directory.Exists(Path.Combine(_folder, "split-work")));
    }

    /// <summary>
    /// One file with both models and the migration history of v0.7.2: three users with data, a row
    /// of a user that no longer exists, shared option lists, and settings of both kinds.
    /// </summary>
    private async Task SeedSingleFile()
    {
        await using var db = new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite($"{ConnectionString};Pooling=False").Options);
        await db.Database.EnsureCreatedAsync();
        var platform = TestPlatform.For(db);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE __EFMigrationsHistory (MigrationId TEXT NOT NULL PRIMARY KEY, ProductVersion TEXT NOT NULL);"
            + $"INSERT INTO __EFMigrationsHistory VALUES ('{SingleFileSplit.LastSingleFileMigration}', '9.0.10');");
        await DefaultUnits.Ensure(db);

        platform.Users.AddRange(
            new User { Id = Admin, Email = "admin", DisplayName = "Administrator", PasswordHash = "admin-hash", IsAdmin = true, CreatedUtc = new DateTime(2026, 1, 1) },
            new User { Id = Alice, Email = "alice@example.com", PasswordHash = "alice-hash", TimeZone = "Europe/Prague", CreatedUtc = new DateTime(2026, 2, 1) },
            new User { Id = Bob, Email = "bob@example.com", PasswordHash = "bob-hash", CreatedUtc = new DateTime(2026, 3, 1) });
        platform.ApiKeys.Add(new ApiKey { UserId = Alice, Name = "Phone", Prefix = "lmd_abcd", KeyHash = new string('a', 64), ExpiresUtc = new DateTime(2027, 1, 1) });
        platform.PasswordResets.Add(new PasswordReset { UserId = Bob, Token = "reset-token", ExpiresUtc = new DateTime(2026, 11, 21) });
        db.Settings.Add(new Setting { Key = "Ai:Model", Value = "some-model" });
        db.Settings.Add(new Setting { Key = ReminderDayBackfill.FlagKey, Value = "done" });
        db.Patterns.Add(new Pattern { Name = "Digits", PatternValue = "^[0-9]+$" });

        var shared = new TagOptionList { Name = "Shared moods", UserId = null, Options = { new TagOption { Value = "good", DisplayName = "Good" }, new TagOption { Value = "bad", DisplayName = "Bad" } } };
        db.TagOptionLists.Add(new TagOptionList { Name = "Shared unused", UserId = null, Options = { new TagOption { Value = "x", DisplayName = "X" } } });
        var alices = new TagOptionList { Name = "Alice's", UserId = Alice, Options = { new TagOption { Value = "a", DisplayName = "A" } } };

        foreach (var (user, name, list) in new[] { (Alice, "Alice", alices), (Bob, "Bob", shared) })
        {
            var group = new TagGroup { Name = $"{name}'s group", UserId = user };
            var scheme = new ColorScheme { Name = $"{name}'s colours", UserId = user, Entries = { new ColorSchemeEntry { Color = "#ff0000", SortOrder = 1, Label = "Low" } } };
            var tag = new Tag { TagName = $"{name}'s tag", UserId = user, Group = group, ColorScheme = scheme };
            var computed = new Tag { TagName = $"{name}'s total", UserId = user, IsComputed = true };
            var recordType = new Tag { TagName = $"{name}'s workout", UserId = user, IsRecordType = true };
            db.Tags.AddRange(tag, computed, recordType);
            if (user == Bob)
            {
                db.Tags.Add(new Tag { TagName = "Bob's mood", UserId = user, OptionList = list });
            }
            else
            {
                db.TagOptionLists.Add(list);
            }

            var trigger = new TagTrigger { UserId = user, Name = "Trigger", IfTag = tag, IfOperand = "1", PromptTag = computed };
            db.TagTriggers.Add(trigger);
            db.TagRules.Add(new TagRule
            {
                UserId = user,
                Name = "Total",
                TargetTag = computed,
                Sources = { new TagRuleSource { SourceTag = tag, Factor = 0.5 } },
                Cases = { new TagRuleCase { SortOrder = 1, Operand = "100", ResultValue = "high" } }
            });
            db.TagChecks.Add(new TagCheck { UserId = user, Name = "Check", IfTag = tag, ThenTag = computed });
            db.TagDayLocks.Add(new TagDayLock { UserId = user, Tag = tag, Date = new DateOnly(2026, 11, 19), IsLocked = true });
            db.Records.Add(new Domain.Entities.Record { UserId = user, RecordType = recordType, DateStarted = new DateTime(2026, 11, 20, 7, 15, 0), DateCreated = DateTime.UtcNow });
            db.TodoLists.Add(new TodoList { UserId = user, Name = "Errands", Items = { new TodoItem { Title = "One" }, new TodoItem { Title = "Two" } } });
            db.Reminders.Add(new Reminder
            {
                UserId = user,
                Title = "Reminder",
                Trigger = trigger,
                Days = { new ReminderDay { UserId = user, Date = new DateOnly(2026, 11, 20), IsDone = true } }
            });
            db.EventLogs.Add(new EventLog { UserId = user, Message = "Something failed", Detail = new EventLogDetail { EventLogId = 0, Description = "Stack trace" } });
            await db.SaveChangesAsync();

            db.Activities.Add(new Activity { UserId = user, TagId = tag.Id, DateStarted = new DateTime(2026, 11, 20, 8, 0, 0) });
            db.ScanMappings.Add(new ScanMapping { UserId = user, CodeValue = "8590000000001", TagId = tag.Id });
            await db.SaveChangesAsync();
        }

        // The user of this row was deleted while their data stayed, as v0.7.2 left it.
        var anyTag = await db.Tags.Select(t => t.Id).FirstAsync();
        db.Activities.Add(new Activity { UserId = Nobody, TagId = anyTag, DateStarted = new DateTime(2025, 1, 1) });
        await db.SaveChangesAsync();
    }

    private static bool Same(object? stored, Guid id)
    {
        return stored is string text && Guid.TryParse(text, out var parsed) && parsed == id;
    }

    private static string Text(Dictionary<string, object?> row)
    {
        return string.Join("|", row.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}={c.Value}"));
    }

    private static List<string> ListNames(string path)
    {
        return Rows(path, "LogMyDay_TagOptionLists").Select(r => (string)r["Name"]!).ToList();
    }

    private static List<string> Tables(string path)
    {
        return Query(path, "SELECT name FROM sqlite_master WHERE type = 'table' AND name LIKE 'LogMyDay~_%' ESCAPE '~' ORDER BY name;")
            .Select(r => (string)r["name"]!)
            .ToList();
    }

    private static List<Dictionary<string, object?>> Rows(string path, string table)
    {
        return Query(path, $"SELECT * FROM \"{table}\" ORDER BY 1;");
    }

    private static List<Dictionary<string, object?>> Query(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();

        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            rows.Add(Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? null : reader.GetValue(i)));
        }

        return rows;
    }

    private static void Execute(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
