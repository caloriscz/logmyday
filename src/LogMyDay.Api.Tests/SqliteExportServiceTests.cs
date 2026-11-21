using System.Globalization;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// The SQLite export against a SQLite source file on this version's schema. Every table holds
/// rows, so the comparison covers every column type and value conversion of the model.
/// </summary>
public sealed class SqliteExportServiceTests : IDisposable
{
    private readonly string _sourcePath = TempDatabasePath("source");
    private readonly string _exportPath = TempDatabasePath("export");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_sourcePath);
        File.Delete(_exportPath);
    }

    [Fact]
    public async Task Export_CopiesEveryRowOfEveryTable_IntoAFileLogMyDayRunsOn()
    {
        await using var source = OpenDatabase(_sourcePath);
        var seeded = await SeedEveryTable(source);

        var service = new SqliteExportService(source, TestPlatform.For(source), NullLogger<SqliteExportService>.Instance);
        string temporaryFile;
        IReadOnlyDictionary<string, int> rowCounts;
        await using (var export = await service.ExportDatabase(CancellationToken.None))
        {
            temporaryFile = ((FileStream)export.Content).Name;
            rowCounts = export.RowCounts;
            Assert.EndsWith(".db", export.FileName);
            Assert.Empty(export.UnknownMigrations);

            await using var file = File.Create(_exportPath);
            await export.Content.CopyToAsync(file);
        }

        Assert.False(File.Exists(temporaryFile), "Disposing the export deletes its temporary file.");

        await using var exported = OpenDatabase(_exportPath);
        Assert.Empty(await exported.Database.GetPendingMigrationsAsync());

        foreach (var entityType in source.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName()!;
            var sourceRows = await RowsOf(source, entityType.ClrType);
            var exportedRows = await RowsOf(exported, entityType.ClrType);

            Assert.True(sourceRows.Count > 0, $"The test seeds no rows into {table}.");
            Assert.Equal(sourceRows.Count, rowCounts[table]);
            Assert.Equal(sourceRows, exportedRows);
        }

        // EF finds rows by Guid and date in the file, so it stores them the way EF itself does.
        var friend = await TestPlatform.For(exported).Users.SingleAsync(u => u.Id == seeded.FriendId);
        Assert.Equal("friend-hash", friend.PasswordHash);
        Assert.Equal(1, await exported.Activities.CountAsync(a => a.UserId == seeded.FriendId && a.DateStarted >= new DateTime(2026, 11, 20)));
        Assert.False((await exported.ScanMappings.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Export_RefusesADatabaseThatLacksMigrationsOfThisVersion()
    {
        await using var source = OpenDatabase(_sourcePath);
        await CreateSchema(source, migrationsLeftOut: 1);

        var service = new SqliteExportService(source, TestPlatform.For(source), NullLogger<SqliteExportService>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportDatabase(CancellationToken.None));
        Assert.Contains("lacks migrations", error.Message);
        Assert.Contains(source.Database.GetMigrations().Last(), error.Message);
    }

    [Fact]
    public async Task Export_ReportsAppliedMigrationsThisVersionDoesNotKnow_AndLeavesThemOutOfTheFile()
    {
        // As in databases that applied a migration whose file was later removed.
        const string unknown = "20260213105416_RenameAppSettingToSetting";
        await using var source = OpenDatabase(_sourcePath);
        await CreateSchema(source);
        var history = source.GetService<IHistoryRepository>();
        await source.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(unknown, ProductInfo.GetVersion())));

        var service = new SqliteExportService(source, TestPlatform.For(source), NullLogger<SqliteExportService>.Instance);
        await using (var export = await service.ExportDatabase(CancellationToken.None))
        {
            Assert.Equal([unknown], export.UnknownMigrations);

            await using var file = File.Create(_exportPath);
            await export.Content.CopyToAsync(file);
        }

        await using var exported = OpenDatabase(_exportPath);
        Assert.Equal(exported.Database.GetMigrations(), await exported.Database.GetAppliedMigrationsAsync());
    }

    private static string TempDatabasePath(string name) =>
        Path.Combine(Path.GetTempPath(), $"lmd-export-test-{name}-{Guid.NewGuid():N}.db");

    private static LogMyDayDbContext OpenDatabase(string path)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseSqlite($"Data Source={path}")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new LogMyDayDbContext(options);
    }

    /// <summary>
    /// The current schema with the migrations recorded as applied, leaving out the newest ones
    /// when asked. The oldest migrations were scaffolded for SQL Server and can't build a new
    /// SQLite file themselves.
    /// </summary>
    private static async Task CreateSchema(LogMyDayDbContext db, int migrationsLeftOut = 0)
    {
        await db.Database.EnsureCreatedAsync();

        var history = db.GetService<IHistoryRepository>();
        await db.Database.ExecuteSqlRawAsync(history.GetCreateScript());

        var migrations = db.Database.GetMigrations().ToList();
        foreach (var migration in migrations.Take(migrations.Count - migrationsLeftOut))
        {
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(migration, ProductInfo.GetVersion())));
        }
    }

    private sealed record Seeded(Guid FriendId);

    /// <summary>
    /// Creates and seeds the source the way startup does, then adds at least one row to every
    /// other table, with non-default values where a conversion could lose them.
    /// </summary>
    private static async Task<Seeded> SeedEveryTable(LogMyDayDbContext db)
    {
        await CreateSchema(db);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("admin-hash");
        await new DatabaseSeeder(db, TestPlatform.For(db), hasher.Object, NullLogger<DatabaseSeeder>.Instance).SeedAsync();

        var admin = await TestPlatform.For(db).Users.SingleAsync();
        var friend = new User { Email = "friend@example.com", PasswordHash = "friend-hash", TimeZone = "Europe/Prague" };
        TestPlatform.For(db).Users.Add(friend);
        db.Patterns.Add(new Pattern { Name = "Digits", PatternValue = "^[0-9]+$" });

        var milligram = await db.Units.SingleAsync(u => u.Key == "milligram");
        var inputTypeId = await db.InputTypes.Select(t => t.Id).FirstAsync();
        var group = new TagGroup { Name = "Supplements", UserId = friend.Id, DisplayOrder = 2, DateCreated = new DateTime(2026, 1, 2, 3, 4, 5) };
        var scheme = new ColorScheme
        {
            Name = "Traffic",
            UserId = friend.Id,
            Entries = { new ColorSchemeEntry { Color = "#ff0000", SortOrder = 1, RangeFrom = 0.5, RangeTo = 2.75, Label = "Low" } }
        };
        var moods = new TagOptionList { Name = "Moods", UserId = null, Options = { new TagOption { Value = "good", DisplayName = "Good" } } };
        var magnesium = new Tag
        {
            TagName = "Magnesium",
            UserId = friend.Id,
            InputTypeId = inputTypeId,
            UnitId = milligram.Id,
            Group = group,
            ColorScheme = scheme,
            MinValue = 0,
            MaxValue = 1000.5,
            Step = 0.25,
            IsRepeatable = false,
            TimeGranularity = TimeGranularity.Exact
        };
        var mood = new Tag { TagName = "Mood", UserId = friend.Id, OptionList = moods };
        var total = new Tag { TagName = "Magnesium total", UserId = friend.Id, IsComputed = true };
        var workout = new Tag { TagName = "Workout", UserId = friend.Id, IsRecordType = true };
        var reps = new Tag { TagName = "Workout: reps", UserId = friend.Id, RecordType = workout, RecordFieldOrder = 1 };
        db.Tags.AddRange(magnesium, mood, total, workout, reps);

        var rule = new TagRule
        {
            UserId = friend.Id,
            Name = "Magnesium total",
            Template = TagRuleTemplate.Conditional,
            TargetTag = total,
            IgnoreZero = false,
            EffectiveFrom = new DateOnly(2026, 11, 1),
            Sources = { new TagRuleSource { SourceTag = magnesium, Factor = 0.5 } },
            Cases = { new TagRuleCase { SortOrder = 1, Operator = TagRuleOperator.GreaterOrEqual, Operand = "100", ResultValue = "high" } }
        };
        var trigger = new TagTrigger
        {
            UserId = friend.Id,
            Name = "Mood after magnesium",
            IfTag = magnesium,
            IfOperator = TagRuleOperator.Greater,
            IfOperand = "50",
            PromptTag = mood,
            Timing = TagTriggerTiming.AtTime,
            AtTime = new TimeOnly(20, 30)
        };
        db.TagRules.Add(rule);
        db.TagTriggers.Add(trigger);
        db.TagChecks.Add(new TagCheck { UserId = friend.Id, Name = "Mood logged", Severity = TagCheckSeverity.Warn, IfTag = magnesium, ThenTag = mood, ThenOperator = TagRuleOperator.IsNotLogged });

        var record = new Domain.Entities.Record { UserId = friend.Id, RecordType = workout, DateStarted = new DateTime(2026, 11, 20, 7, 15, 0), DateCreated = DateTime.UtcNow };
        db.Records.Add(record);

        db.TagDayLocks.Add(new TagDayLock { UserId = friend.Id, Tag = magnesium, Date = new DateOnly(2026, 11, 19), IsLocked = true, SetBy = DayLockSetBy.Auto, Reason = "Done" });
        db.TodoLists.Add(new TodoList
        {
            UserId = friend.Id,
            Name = "Errands",
            ShowOnHomepage = true,
            AutoLogMode = AutoLogMode.ResetIfExists,
            CompletionTag = mood,
            Items =
            {
                new TodoItem
                {
                    Title = "Buy magnesium",
                    StartDate = new DateTime(2026, 11, 18),
                    DueDate = new DateTime(2026, 11, 21),
                    NotifyAt = new TimeOnly(9, 45),
                    IsDone = true,
                    DoneAt = new DateTime(2026, 11, 20, 10, 0, 0),
                    RecurrenceType = RecurrenceType.Weekly
                }
            }
        });
        db.Reminders.Add(new Reminder
        {
            UserId = friend.Id,
            Title = "Take magnesium",
            NotifyAt = new TimeOnly(21, 0),
            RecurrenceType = RecurrenceType.Daily,
            MonitorFromDate = new DateOnly(2026, 11, 1),
            AllowUnfilled = true,
            Trigger = trigger,
            TriggerDay = new DateOnly(2026, 11, 20),
            Days = { new ReminderDay { UserId = friend.Id, Date = new DateOnly(2026, 11, 20), IsDone = true, DoneAt = new DateTime(2026, 11, 20, 21, 5, 0), CompletionValue = "125" } }
        });
        TestPlatform.For(db).ApiKeys.Add(new ApiKey { UserId = friend.Id, Name = "Phone", Prefix = "lmd_abcd", KeyHash = new string('a', 64), Scope = ApiKeyScope.ReadWrite, ExpiresUtc = new DateTime(2027, 1, 1) });
        TestPlatform.For(db).PasswordResets.Add(new PasswordReset { UserId = friend.Id, Token = "reset-token", ExpiresUtc = new DateTime(2026, 11, 21) });
        db.EventLogs.Add(new EventLog { UserId = admin.Id, Level = EventLogLevel.Error, Message = "Something failed", Detail = new EventLogDetail { EventLogId = 0, Description = "Stack trace" } });
        db.Settings.Add(new Setting { Key = "Test:Flag", Value = "done", Description = "One-time migration flag" });
        await db.SaveChangesAsync();

        db.ScanMappings.Add(new ScanMapping { UserId = friend.Id, CodeValue = "8590000000001", CodeType = CodeType.QRCode, TagId = magnesium.Id, IsActive = false });
        db.Activities.AddRange(
            new Activity { UserId = friend.Id, TagId = magnesium.Id, DateStarted = new DateTime(2026, 11, 20, 8, 0, 0), DateFinished = new DateTime(2026, 11, 20, 8, 30, 0), Description = "125.5" },
            new Activity { UserId = friend.Id, TagId = total.Id, DateStarted = new DateTime(2026, 11, 19), Description = "62.75", RuleId = rule.Id, WindowKey = "2026-11-19" },
            new Activity { UserId = friend.Id, TagId = reps.Id, DateStarted = record.DateStarted.AddDays(-1), Description = "12", RecordId = record.Id },
            new Activity { UserId = admin.Id, TagId = mood.Id, DateStarted = new DateTime(2026, 11, 20, 12, 0, 0), Description = "good" });
        await db.SaveChangesAsync();

        return new Seeded(friend.Id);
    }

    private static async Task<List<string>> RowsOf(LogMyDayDbContext db, Type clrType)
    {
        var method = typeof(SqliteExportServiceTests)
            .GetMethod(nameof(RowsOfType), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(clrType);

        return await (Task<List<string>>)method.Invoke(null, [db])!;
    }

    /// <summary>Every row as one line of property=value pairs, sorted, for an exact comparison.</summary>
    private static async Task<List<string>> RowsOfType<TEntity>(LogMyDayDbContext db) where TEntity : class
    {
        var properties = db.Model.FindEntityType(typeof(TEntity))!.GetProperties().ToList();
        var rows = await db.Set<TEntity>().AsNoTracking().ToListAsync();

        return rows
            .Select(row => string.Join("|", properties.Select(p => $"{p.Name}={Format(p.GetGetter().GetClrValueUsingContainingEntity(row))}")))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };
}
