using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

public class TagRuleTests
{
    private sealed class Fixture
    {
        public required LogMyDayDbContext Context { get; init; }
        public required ActivityService Activities { get; init; }
        public required TagRuleService Rules { get; init; }
        public required Guid UserId { get; init; }
        public DateOnly Today { get; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateTime TodayAt(int hour) => Today.ToDateTime(new TimeOnly(hour, 0));

        public async Task<Tag> AddTag(string name, int inputTypeId = 1, Guid? owner = null, bool repeatable = true)
        {
            var tag = new Tag
            {
                TagName = name,
                InputTypeId = inputTypeId,
                IsRequired = false,
                IsRepeatable = repeatable,
                TimeGranularity = TimeGranularity.Daily,
                UserId = owner ?? UserId
            };
            Context.Tags.Add(tag);
            await Context.SaveChangesAsync();

            return tag;
        }

        public Task<ActivityResponse> Log(Tag tag, DateTime when, string value) =>
            Activities.Create(new ActivityRequest { PrimaryTagId = tag.Id, DateStarted = when, Description = value }, UserId);

        public Task<TagRuleResponse> CreateRule(Tag target, params (Tag Tag, double Factor)[] sources) =>
            Rules.Create(new TagRuleRequest
            {
                Name = "Total",
                TargetTagId = target.Id,
                Sources = sources.Select(s => new TagRuleSourceRequest { SourceTagId = s.Tag.Id, Factor = s.Factor }).ToList()
            }, UserId);

        public async Task<List<Activity>> Results(Tag target)
        {
            Context.ChangeTracker.Clear();

            return await Context.Activities.Where(a => a.TagId == target.Id).OrderBy(a => a.DateStarted).ToListAsync();
        }
    }

    private static Fixture CreateFixture(string dbName)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return CreateFixture(new LogMyDayDbContext(options));
    }

    // A real SQLite database: transactions, the filtered unique index and query translation run
    // as they do in production. The open connection keeps the in-memory database alive.
    private static Fixture CreateSqliteFixture(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new LogMyDayDbContext(options);
        context.Database.EnsureCreated();

        return CreateFixture(context);
    }

    private static Fixture CreateFixture(LogMyDayDbContext context)
    {
        var userId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = "t@t", PasswordHash = "x", TimeZone = "UTC", Culture = "en-US" });
        context.SaveChanges();

        var engine = new TagRuleEngine(context);
        var eventLog = new EventLogService(context, NullLogger<EventLogService>.Instance);

        return new Fixture
        {
            Context = context,
            Activities = new ActivityService(context, new ActivityRepository(context), eventLog, new TagDayLockService(context), engine),
            Rules = new TagRuleService(context, engine, eventLog),
            UserId = userId
        };
    }

    [Fact]
    public async Task SourceWrites_KeepTheDailyWeightedSumInLine()
    {
        var f = CreateFixture(nameof(SourceWrites_KeepTheDailyWeightedSumInLine));
        var a = await f.AddTag("A");
        var b = await f.AddTag("B", inputTypeId: 6);
        var total = await f.AddTag("Total", inputTypeId: 6);
        await f.CreateRule(total, (a, 2), (b, 0.5));

        var first = await f.Log(a, f.TodayAt(8), "3");
        await f.Log(b, f.TodayAt(9), "4");

        var result = Assert.Single(await f.Results(total));
        Assert.Equal("8", result.Description); // 2×3 + 0.5×4
        Assert.NotNull(result.RuleId);
        Assert.Equal(f.Today.ToString("yyyy-MM-dd"), result.WindowKey);
        Assert.Equal(f.Today.ToDateTime(TimeOnly.MinValue), result.DateStarted);

        // Edit replaces the total; it never accumulates.
        await f.Activities.Update(first.Id, new ActivityRequest { PrimaryTagId = a.Id, DateStarted = f.TodayAt(8), Description = "5" }, f.UserId);
        Assert.Equal("12", Assert.Single(await f.Results(total)).Description);

        // Moving a source to another (later) day splits the total across both days.
        await f.Activities.Update(first.Id, new ActivityRequest { PrimaryTagId = a.Id, DateStarted = f.TodayAt(8).AddDays(1), Description = "5" }, f.UserId);
        var split = await f.Results(total);
        Assert.Equal(["2", "10"], split.Select(r => r.Description));

        // A day that loses its last source loses its result: no zero rows.
        await f.Activities.Delete(first.Id, f.UserId);
        Assert.Equal("2", Assert.Single(await f.Results(total)).Description);
    }

    [Fact]
    public async Task OnSqlite_SourceWritesAndRuleEdits_KeepOneResultPerDay()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var f = CreateSqliteFixture(connection);
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total", inputTypeId: 6);
        var rule = await f.CreateRule(total, (a, 2));

        var first = await f.Log(a, f.TodayAt(8), "3");
        await f.Log(a, f.TodayAt(9), "1");
        await f.Activities.Update(first.Id, new ActivityRequest { PrimaryTagId = a.Id, DateStarted = f.TodayAt(8), Description = "4" }, f.UserId);
        await f.Rules.Update(rule.Id, new TagRuleRequest
        {
            Name = "Total",
            TargetTagId = total.Id,
            Sources = [new() { SourceTagId = a.Id, Factor = 0.5 }]
        }, f.UserId);

        Assert.Equal("2.5", Assert.Single(await f.Results(total)).Description); // 0.5 × (4 + 1)
        await Assert.ThrowsAsync<TagComputedException>(() => f.Log(total, f.TodayAt(10), "1"));

        await f.Rules.Delete(rule.Id, f.UserId);
        Assert.Empty(await f.Results(total));
    }

    [Fact]
    public async Task AccumulatePath_AlsoUpdatesTheResult()
    {
        var f = CreateFixture(nameof(AccumulatePath_AlsoUpdatesTheResult));
        var a = await f.AddTag("A", repeatable: false);
        var total = await f.AddTag("Total");
        await f.CreateRule(total, (a, 10));

        await f.Log(a, f.TodayAt(8), "1");
        await f.Log(a, f.TodayAt(12), "2"); // non-repeatable numeric: adds to the day's row

        Assert.Equal("30", Assert.Single(await f.Results(total)).Description);
    }

    [Fact]
    public async Task IgnoreZero_DayWithOnlyZeroRowsHasNoResult()
    {
        var f = CreateFixture(nameof(IgnoreZero_DayWithOnlyZeroRowsHasNoResult));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        await f.CreateRule(total, (a, 1));

        await f.Log(a, f.TodayAt(8), "0");

        Assert.Empty(await f.Results(total));
    }

    [Fact]
    public async Task DaysBeforeEffectiveFrom_AreNotEvaluated()
    {
        var f = CreateFixture(nameof(DaysBeforeEffectiveFrom_AreNotEvaluated));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        await f.CreateRule(total, (a, 1));

        await f.Log(a, f.TodayAt(8).AddDays(-1), "5");

        Assert.Empty(await f.Results(total));
    }

    [Fact]
    public async Task CreatingARule_EvaluatesToday()
    {
        var f = CreateFixture(nameof(CreatingARule_EvaluatesToday));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        await f.Log(a, f.TodayAt(8), "4");

        await f.CreateRule(total, (a, 1.5));

        Assert.Equal("6", Assert.Single(await f.Results(total)).Description);
    }

    [Fact]
    public async Task ComputedTag_RefusesManualWrites()
    {
        var f = CreateFixture(nameof(ComputedTag_RefusesManualWrites));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        await f.CreateRule(total, (a, 1));
        await f.Log(a, f.TodayAt(8), "4");
        var generated = Assert.Single(await f.Results(total));

        await Assert.ThrowsAsync<TagComputedException>(() => f.Log(total, f.TodayAt(9), "1"));
        await Assert.ThrowsAsync<TagComputedException>(() =>
            f.Activities.Update(generated.Id, new ActivityRequest { PrimaryTagId = total.Id, DateStarted = f.TodayAt(9), Description = "1" }, f.UserId));
        await Assert.ThrowsAsync<TagComputedException>(() => f.Activities.Delete(generated.Id, f.UserId));
    }

    [Fact]
    public async Task Validation_RejectsTargetsAndSourcesThatBreakTheRules()
    {
        var f = CreateFixture(nameof(Validation_RejectsTargetsAndSourcesThatBreakTheRules));
        var a = await f.AddTag("A");
        var text = await f.AddTag("Text", inputTypeId: 2);
        var used = await f.AddTag("Used");
        await f.Log(used, f.TodayAt(8), "1");
        var foreign = await f.AddTag("Foreign", owner: Guid.NewGuid());
        var total = await f.AddTag("Total");

        await Assert.ThrowsAsync<ArgumentException>(() => f.CreateRule(total, (text, 1)));        // non-numeric source
        await Assert.ThrowsAsync<ArgumentException>(() => f.CreateRule(text, (a, 1)));            // non-numeric target
        await Assert.ThrowsAsync<ArgumentException>(() => f.CreateRule(used, (a, 1)));            // target has values
        await Assert.ThrowsAsync<ArgumentException>(() => f.CreateRule(total, (total, 1)));       // target is a source
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.CreateRule(total, (foreign, 1)));  // not the caller's tag

        await f.CreateRule(total, (a, 1));
        var grandTotal = await f.AddTag("Grand total");
        await Assert.ThrowsAsync<ArgumentException>(() => f.CreateRule(grandTotal, (total, 1))); // no chaining
    }

    [Fact]
    public async Task UpdatingFactors_RewritesTheActiveRange()
    {
        var f = CreateFixture(nameof(UpdatingFactors_RewritesTheActiveRange));
        var a = await f.AddTag("A");
        var b = await f.AddTag("B");
        var total = await f.AddTag("Total");
        var rule = await f.CreateRule(total, (a, 1));
        await f.Log(a, f.TodayAt(8), "2");
        await f.Log(b, f.TodayAt(9), "3");

        await f.Rules.Update(rule.Id, new TagRuleRequest
        {
            Name = "Total",
            TargetTagId = total.Id,
            Sources = [new() { SourceTagId = a.Id, Factor = 10 }, new() { SourceTagId = b.Id, Factor = 1 }]
        }, f.UserId);

        Assert.Equal("23", Assert.Single(await f.Results(total)).Description);
    }

    [Fact]
    public async Task DeletingARule_RemovesItsResultsAndFreesTheTag()
    {
        var f = CreateFixture(nameof(DeletingARule_RemovesItsResultsAndFreesTheTag));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        var rule = await f.CreateRule(total, (a, 1));
        await f.Log(a, f.TodayAt(8), "2");

        await f.Rules.Delete(rule.Id, f.UserId);

        Assert.Empty(await f.Results(total));
        Assert.False((await f.Context.Tags.FindAsync(total.Id))!.IsComputed);
        await f.Log(total, f.TodayAt(9), "1"); // a normal tag again
    }

    [Fact]
    public async Task TagDelete_IsRefusedWhileARuleUsesTheTag()
    {
        var f = CreateFixture(nameof(TagDelete_IsRefusedWhileARuleUsesTheTag));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        await f.CreateRule(total, (a, 1));
        var tags = new TagService(f.Context, new TagRepository(f.Context), NullLogger<TagService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tags.Delete(a.Id, f.UserId));
        Assert.Contains("'Total'", ex.Message);
    }

    [Fact]
    public async Task PausedRule_KeepsResultsButStopsUpdating()
    {
        var f = CreateFixture(nameof(PausedRule_KeepsResultsButStopsUpdating));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        var rule = await f.CreateRule(total, (a, 1));
        await f.Log(a, f.TodayAt(8), "2");

        await f.Rules.Update(rule.Id, new TagRuleRequest
        {
            Name = "Total",
            TargetTagId = total.Id,
            IsEnabled = false,
            Sources = [new() { SourceTagId = a.Id, Factor = 1 }]
        }, f.UserId);
        await f.Log(a, f.TodayAt(9), "5");

        Assert.Equal("2", Assert.Single(await f.Results(total)).Description);
    }
}
