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

        // History across a month boundary, recomputed one month per transaction.
        await f.Log(a, f.TodayAt(8).AddDays(-45), "6");
        await f.Rules.Recompute(rule.Id, new TagRuleRecomputeRequest { From = f.Today.AddDays(-45), To = f.Today }, f.UserId);
        Assert.Equal(["3", "2.5"], (await f.Results(total)).Select(r => r.Description));

        await f.Rules.Delete(rule.Id, f.UserId);
        Assert.Empty(await f.Results(total));
    }

    [Fact]
    public async Task Preview_CountsWhatARecomputeWouldDo_WithoutWriting()
    {
        var f = CreateFixture(nameof(Preview_CountsWhatARecomputeWouldDo_WithoutWriting));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        // History over 40 days, so the range always crosses a month boundary.
        foreach (var daysAgo in new[] { 40, 20, 1 })
        {
            await f.Log(a, f.TodayAt(8).AddDays(-daysAgo), "2");
        }
        await f.Log(a, f.TodayAt(9).AddDays(-20), "oops");
        var rule = await f.CreateRule(total, (a, 3));

        var preview = await f.Rules.Preview(rule.Id, from: null, to: null, f.UserId);

        Assert.Equal(f.Today.AddDays(-40), preview.From);
        Assert.Equal(f.Today, preview.To);
        Assert.Equal(f.Today.AddDays(-40), preview.FirstSourceDate);
        Assert.Equal(3, preview.Created);
        Assert.Equal(0, preview.Updated + preview.Deleted + preview.Unchanged);
        Assert.Equal(1, preview.SkippedSourceValues);
        Assert.Empty(await f.Results(total)); // nothing written
    }

    [Fact]
    public async Task Recompute_WritesHistory_IsIdempotent_AndMovesEffectiveFromBack()
    {
        var f = CreateFixture(nameof(Recompute_WritesHistory_IsIdempotent_AndMovesEffectiveFromBack));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        var old = await f.Log(a, f.TodayAt(8).AddDays(-40), "2");
        await f.Log(a, f.TodayAt(8).AddDays(-5), "1");
        var rule = await f.CreateRule(total, (a, 3));
        var range = new TagRuleRecomputeRequest { From = f.Today.AddDays(-40), To = f.Today };

        var first = await f.Rules.Recompute(rule.Id, range, f.UserId);
        Assert.Equal(2, first.Created);
        Assert.Equal(["6", "3"], (await f.Results(total)).Select(r => r.Description));

        var second = await f.Rules.Recompute(rule.Id, range, f.UserId);
        Assert.Equal(0, second.Created + second.Updated + second.Deleted);
        Assert.Equal(2, second.Unchanged);

        Assert.Equal(f.Today.AddDays(-40), (await f.Rules.GetById(rule.Id, f.UserId)).EffectiveFrom);
        // The recomputed past now follows source edits.
        await f.Activities.Update(old.Id, new ActivityRequest { PrimaryTagId = a.Id, DateStarted = old.DateStarted, Description = "10" }, f.UserId);
        Assert.Equal("30", (await f.Results(total)).First().Description);
    }

    [Fact]
    public async Task PartialRecomputeAfterAFactorChange_MatchesItsPreview()
    {
        var f = CreateFixture(nameof(PartialRecomputeAfterAFactorChange_MatchesItsPreview));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        foreach (var daysAgo in new[] { 30, 20, 10, 5 })
        {
            await f.Log(a, f.TodayAt(8).AddDays(-daysAgo), "1");
        }
        var rule = await f.CreateRule(total, (a, 1));
        await f.Rules.Recompute(rule.Id, new TagRuleRecomputeRequest { From = f.Today.AddDays(-30), To = f.Today }, f.UserId);

        await f.Rules.Update(rule.Id, new TagRuleRequest
        {
            Name = "Total",
            TargetTagId = total.Id,
            Sources = [new() { SourceTagId = a.Id, Factor = 2 }]
        }, f.UserId);
        var from = f.Today.AddDays(-12);
        var preview = await f.Rules.Preview(rule.Id, from, f.Today, f.UserId);
        var run = await f.Rules.Recompute(rule.Id, new TagRuleRecomputeRequest { From = from, To = f.Today }, f.UserId);

        Assert.Equal(2, preview.Updated);             // days -10 and -5
        Assert.Equal(2, preview.ResultsBeforeFrom);   // days -30 and -20 keep factor 1
        Assert.Equal(preview.Created, run.Created);
        Assert.Equal(preview.Updated, run.Updated);
        Assert.Equal(preview.Deleted, run.Deleted);
        Assert.Equal(["1", "1", "2", "2"], (await f.Results(total)).Select(r => r.Description));
    }

    [Fact]
    public async Task Recompute_RefusesPausedRulesAndFutureDays()
    {
        var f = CreateFixture(nameof(Recompute_RefusesPausedRulesAndFutureDays));
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total");
        var rule = await f.CreateRule(total, (a, 1));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            f.Rules.Recompute(rule.Id, new TagRuleRecomputeRequest { From = f.Today, To = f.Today.AddDays(1) }, f.UserId));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            f.Rules.Recompute(rule.Id, new TagRuleRecomputeRequest { From = f.Today, To = f.Today.AddDays(-1) }, f.UserId));

        await f.Rules.Update(rule.Id, new TagRuleRequest
        {
            Name = "Total",
            TargetTagId = total.Id,
            IsEnabled = false,
            Sources = [new() { SourceTagId = a.Id, Factor = 1 }]
        }, f.UserId);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            f.Rules.Recompute(rule.Id, new TagRuleRecomputeRequest { From = f.Today, To = f.Today }, f.UserId));
    }

    [Fact]
    public async Task RepeatableSourceSeveralTimesADay_AndANegativeFactor_SumPerDay()
    {
        var f = CreateFixture(nameof(RepeatableSourceSeveralTimesADay_AndANegativeFactor_SumPerDay));
        var dose = await f.AddTag("Dose", repeatable: true);   // logged morning and afternoon
        var loss = await f.AddTag("Loss", inputTypeId: 6);     // subtracted
        var net = await f.AddTag("Net", inputTypeId: 6);
        await f.CreateRule(net, (dose, 1), (loss, -0.5));

        await f.Log(dose, f.TodayAt(8), "144");
        await f.Log(dose, f.TodayAt(14), "96");
        await f.Log(loss, f.TodayAt(20), "10");

        Assert.Equal("235", Assert.Single(await f.Results(net)).Description); // 144 + 96 − 0.5 × 10
    }

    [Fact]
    public async Task OnSqlite_BackupRoundTrip_RestoresRulesAndRecomputesTheirValues()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var f = CreateSqliteFixture(connection);
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total", inputTypeId: 6);
        var rule = await f.CreateRule(total, (a, 2));
        foreach (var daysAgo in new[] { 40, 3, 0 })
        {
            await f.Log(a, f.TodayAt(8).AddDays(-daysAgo), "3");
        }
        await f.Rules.Recompute(rule.Id, new TagRuleRecomputeRequest { From = f.Today.AddDays(-40), To = f.Today }, f.UserId);
        var before = (await f.Results(total)).Select(r => (r.WindowKey, r.Description)).ToList();
        Assert.Equal(3, before.Count);

        var backups = new BackupService(f.Context, NullLogger<BackupService>.Instance, new TagRuleEngine(f.Context));

        // Full backup: the rule is exported by tag name, its values are not.
        var export = await backups.ExportDataAsync(f.UserId);
        var exportedRule = Assert.Single(export.TagRules);
        Assert.Equal("Total", exportedRule.TargetTagName);
        Assert.Equal(("A", 2.0), (exportedRule.Sources.Single().SourceTagName, exportedRule.Sources.Single().Factor));
        Assert.DoesNotContain(export.Activities, x => x.TagName == "Total");

        await backups.ClearDataAsync(f.UserId);
        Assert.False(await f.Context.TagRules.AnyAsync());

        var imported = await backups.ImportDataAsync(export, clearExistingData: false, f.UserId);
        Assert.True(imported.Success, string.Join("; ", imported.Errors));
        Assert.Equal(1, imported.Statistics.TagRulesImported);
        Assert.Equal(3, imported.Statistics.TagRuleValuesComputed);

        var restoredTotal = await f.Context.Tags.SingleAsync(t => t.TagName == "Total" && t.UserId == f.UserId);
        Assert.True(restoredTotal.IsComputed);
        Assert.Equal(before, (await f.Results(restoredTotal)).Select(r => (r.WindowKey, r.Description)).ToList());

        // Secure backup: the same round trip through the per-user format.
        var secure = await backups.CreateSecureBackup(f.UserId);
        Assert.Single(secure.TagRules);
        await backups.ClearUserData(f.UserId);
        var restored = await backups.RestoreSecureBackup(secure, f.UserId);
        Assert.True(restored.Success, string.Join("; ", restored.Errors));
        Assert.Equal(3, restored.Statistics.TagRuleValuesComputed);
    }

    [Fact]
    public async Task OnSqlite_BackupRoundTrip_KeepsAPausedRulesValuesAsTheyAre()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var f = CreateSqliteFixture(connection);
        var a = await f.AddTag("A");
        var total = await f.AddTag("Total", inputTypeId: 6);
        var rule = await f.CreateRule(total, (a, 2));
        var logged = await f.Log(a, f.TodayAt(8), "3");
        Assert.Equal("6", Assert.Single(await f.Results(total)).Description);

        // Paused: the value stays at 6 although its source changes afterwards.
        await f.Rules.Update(rule.Id, new TagRuleRequest
        {
            Name = "Total",
            TargetTagId = total.Id,
            IsEnabled = false,
            Sources = [new() { SourceTagId = a.Id, Factor = 2 }]
        }, f.UserId);
        await f.Activities.Update(logged.Id, new ActivityRequest { PrimaryTagId = a.Id, DateStarted = f.TodayAt(8), Description = "10" }, f.UserId);
        Assert.Equal("6", Assert.Single(await f.Results(total)).Description);

        var backups = new BackupService(f.Context, NullLogger<BackupService>.Instance, new TagRuleEngine(f.Context));
        var export = await backups.ExportDataAsync(f.UserId);
        Assert.Equal("6", Assert.Single(Assert.Single(export.TagRules).Values).Value);

        await backups.ClearDataAsync(f.UserId);
        var imported = await backups.ImportDataAsync(export, clearExistingData: false, f.UserId);
        Assert.True(imported.Success, string.Join("; ", imported.Errors));

        var restoredRule = await f.Context.TagRules.SingleAsync();
        Assert.False(restoredRule.IsEnabled);
        var restoredTotal = await f.Context.Tags.SingleAsync(t => t.TagName == "Total" && t.UserId == f.UserId);
        var value = Assert.Single(await f.Results(restoredTotal));
        Assert.Equal("6", value.Description);        // restored as it was, not recomputed to 20
        Assert.Equal(restoredRule.Id, value.RuleId);
    }

    [Fact]
    public async Task BackupImport_SkipsARuleWhoseTagsDoNotFit_WithAWarning()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var f = CreateSqliteFixture(connection);
        await f.AddTag("Total", inputTypeId: 6);
        var backups = new BackupService(f.Context, NullLogger<BackupService>.Instance, new TagRuleEngine(f.Context));

        var result = await backups.ImportDataAsync(new BackupData
        {
            Metadata = new BackupMetadata(),
            TagRules = [new TagRuleBackup { Name = "Orphan", TargetTagName = "Total", Sources = [new() { SourceTagName = "Missing", Factor = 1 }] }]
        }, clearExistingData: false, f.UserId);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal(1, result.Statistics.TagRulesSkipped);
        Assert.Contains(result.Warnings, w => w.Contains("'Missing'"));
        Assert.False(await f.Context.TagRules.AnyAsync());
    }

    private Task<TagRuleResponse> CreateAggregate(Fixture f, Tag target, TagRuleAggregateKind kind, bool ignoreZero, params Tag[] sources) =>
        f.Rules.Create(new TagRuleRequest
        {
            Name = kind.ToString(),
            Template = TagRuleTemplate.Aggregate,
            AggregateKind = kind,
            TargetTagId = target.Id,
            IgnoreZero = ignoreZero,
            Sources = sources.Select(s => new TagRuleSourceRequest { SourceTagId = s.Id, Factor = 7 }).ToList()
        }, f.UserId);

    [Fact]
    public async Task Aggregate_AverageMinimumMaximum_OverScoresPerDay()
    {
        var f = CreateFixture(nameof(Aggregate_AverageMinimumMaximum_OverScoresPerDay));
        var headache = await f.AddTag("Headache", inputTypeId: 10); // score 0-5
        var backPain = await f.AddTag("Back pain", inputTypeId: 10);
        var average = await f.AddTag("Average pain", inputTypeId: 6);
        var minimum = await f.AddTag("Least pain", inputTypeId: 6);
        var maximum = await f.AddTag("Worst pain", inputTypeId: 6);
        var avgRule = await CreateAggregate(f, average, TagRuleAggregateKind.Average, ignoreZero: false, headache, backPain);
        await CreateAggregate(f, minimum, TagRuleAggregateKind.Minimum, ignoreZero: false, headache, backPain);
        await CreateAggregate(f, maximum, TagRuleAggregateKind.Maximum, ignoreZero: false, headache, backPain);

        var morning = await f.Log(headache, f.TodayAt(8), "4");
        await f.Log(headache, f.TodayAt(18), "1");
        await f.Log(backPain, f.TodayAt(9), "2");

        Assert.Equal("2.33", Assert.Single(await f.Results(average)).Description); // (4 + 1 + 2) / 3
        Assert.Equal("1", Assert.Single(await f.Results(minimum)).Description);
        Assert.Equal("4", Assert.Single(await f.Results(maximum)).Description);

        await f.Activities.Update(morning.Id, new ActivityRequest { PrimaryTagId = headache.Id, DateStarted = f.TodayAt(8), Description = "5" }, f.UserId);
        Assert.Equal("2.67", Assert.Single(await f.Results(average)).Description);
        Assert.Equal("5", Assert.Single(await f.Results(maximum)).Description);

        // Factors belong to the weighted sum; an aggregate stores 1.
        Assert.All(avgRule.Sources, s => Assert.Equal(1, s.Factor));
        Assert.Equal("Aggregate", avgRule.Template);
        Assert.Equal("Average", avgRule.AggregateKind);
    }

    [Fact]
    public async Task Aggregate_Count_CountsEntriesOfAnyType_AndIgnoresSkipMarkers()
    {
        var f = CreateFixture(nameof(Aggregate_Count_CountsEntriesOfAnyType_AndIgnoresSkipMarkers));
        var walk = await f.AddTag("Walk", inputTypeId: 3);     // yes/no
        var note = await f.AddTag("Note", inputTypeId: 2);     // text
        var count = await f.AddTag("Entries", inputTypeId: 1);
        await CreateAggregate(f, count, TagRuleAggregateKind.Count, ignoreZero: true, walk, note);

        await f.Log(walk, f.TodayAt(8), "true");
        await f.Log(walk, f.TodayAt(12), "false");   // a skip marker: left out
        await f.Log(note, f.TodayAt(13), "felt fine");

        Assert.Equal("2", Assert.Single(await f.Results(count)).Description);
    }

    [Fact]
    public async Task Aggregate_Validation_ChecksSourceTypesPerCalculation()
    {
        var f = CreateFixture(nameof(Aggregate_Validation_ChecksSourceTypesPerCalculation));
        var text = await f.AddTag("Text", inputTypeId: 2);
        var score = await f.AddTag("Score", inputTypeId: 10);
        var target = await f.AddTag("Result", inputTypeId: 6);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateAggregate(f, target, TagRuleAggregateKind.Average, true, text));
        await Assert.ThrowsAsync<ArgumentException>(() => f.CreateRule(target, (score, 1))); // weighted sum: numbers only
        await Assert.ThrowsAsync<ArgumentException>(() => f.Rules.Create(new TagRuleRequest
        {
            Name = "No kind",
            Template = TagRuleTemplate.Aggregate,
            TargetTagId = target.Id,
            Sources = [new() { SourceTagId = score.Id }]
        }, f.UserId));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Rules.Create(new TagRuleRequest
        {
            Name = "Not yet",
            Template = TagRuleTemplate.Conditional,
            TargetTagId = target.Id,
            Sources = [new() { SourceTagId = score.Id }]
        }, f.UserId));

        var counted = await CreateAggregate(f, target, TagRuleAggregateKind.Count, true, text); // count takes any type
        Assert.Equal("Count", counted.AggregateKind);
    }

    [Fact]
    public async Task OnSqlite_AggregateRule_SurvivesBackupRoundTrip()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var f = CreateSqliteFixture(connection);
        var score = await f.AddTag("Score", inputTypeId: 10);
        var average = await f.AddTag("Average", inputTypeId: 6);
        await CreateAggregate(f, average, TagRuleAggregateKind.Average, ignoreZero: false, score);
        await f.Log(score, f.TodayAt(8), "2");
        await f.Log(score, f.TodayAt(9), "3");

        var backups = new BackupService(f.Context, NullLogger<BackupService>.Instance, new TagRuleEngine(f.Context));
        var export = await backups.ExportDataAsync(f.UserId);
        Assert.Equal(TagRuleTemplate.Aggregate, Assert.Single(export.TagRules).Template);

        await backups.ClearDataAsync(f.UserId);
        var imported = await backups.ImportDataAsync(export, clearExistingData: false, f.UserId);
        Assert.True(imported.Success, string.Join("; ", imported.Errors));

        var rule = await f.Context.TagRules.SingleAsync();
        Assert.Equal((TagRuleTemplate.Aggregate, TagRuleAggregateKind.Average), (rule.Template, rule.AggregateKind!.Value));
        var restored = await f.Context.Tags.SingleAsync(t => t.TagName == "Average" && t.UserId == f.UserId);
        Assert.Equal("2.5", Assert.Single(await f.Results(restored)).Description);
    }

    private static Task<TagRuleResponse> CreateConditional(Fixture f, Tag target, Tag source, bool ignoreZero, params (TagRuleOperator Op, string? Operand, string Result)[] cases) =>
        f.Rules.Create(new TagRuleRequest
        {
            Name = "Conditional",
            Template = TagRuleTemplate.Conditional,
            TargetTagId = target.Id,
            IgnoreZero = ignoreZero,
            Sources = [new() { SourceTagId = source.Id }],
            Cases = cases.Select(c => new TagRuleCaseRequest { Operator = c.Op, Operand = c.Operand, ResultValue = c.Result }).ToList()
        }, f.UserId);

    [Fact]
    public async Task Presence_GivesTheFixedValueOnLoggedDaysOnly()
    {
        var f = CreateFixture(nameof(Presence_GivesTheFixedValueOnLoggedDaysOnly));
        var gym = await f.AddTag("Gym", inputTypeId: 2);
        var exercise = await f.AddTag("Exercise", inputTypeId: 3);
        await CreateConditional(f, exercise, gym, ignoreZero: true, (TagRuleOperator.IsLogged, null, "yes"));

        var visit = await f.Log(gym, f.TodayAt(18), "legs");
        Assert.Equal("true", Assert.Single(await f.Results(exercise)).Description);

        await f.Activities.Delete(visit.Id, f.UserId);
        Assert.Empty(await f.Results(exercise));
    }

    [Fact]
    public async Task Conditional_OrderedCasesOnTheDayTotal_FirstMatchWins()
    {
        var f = CreateFixture(nameof(Conditional_OrderedCasesOnTheDayTotal_FirstMatchWins));
        var coffees = await f.AddTag("Coffees");
        var caffeine = await f.AddTag("Caffeine", inputTypeId: 2);
        await CreateConditional(f, caffeine, coffees, ignoreZero: true,
            (TagRuleOperator.GreaterOrEqual, "4", "Over limit"),
            (TagRuleOperator.GreaterOrEqual, "2", "Normal"),
            (TagRuleOperator.Otherwise, null, "Low"));

        var first = await f.Log(coffees, f.TodayAt(7), "1");
        Assert.Equal("Low", Assert.Single(await f.Results(caffeine)).Description);

        await f.Log(coffees, f.TodayAt(10), "2");                  // day total 3
        Assert.Equal("Normal", Assert.Single(await f.Results(caffeine)).Description);

        await f.Activities.Update(first.Id, new ActivityRequest { PrimaryTagId = coffees.Id, DateStarted = f.TodayAt(7), Description = "2" }, f.UserId);
        Assert.Equal("Over limit", Assert.Single(await f.Results(caffeine)).Description); // total 4
    }

    [Fact]
    public async Task Conditional_NoMatchingCase_LeavesNoValue()
    {
        var f = CreateFixture(nameof(Conditional_NoMatchingCase_LeavesNoValue));
        var coffees = await f.AddTag("Coffees");
        var flag = await f.AddTag("Over", inputTypeId: 3);
        await CreateConditional(f, flag, coffees, ignoreZero: true, (TagRuleOperator.Greater, "3", "yes"));

        var logged = await f.Log(coffees, f.TodayAt(7), "5");
        Assert.Equal("true", Assert.Single(await f.Results(flag)).Description);

        await f.Activities.Update(logged.Id, new ActivityRequest { PrimaryTagId = coffees.Id, DateStarted = f.TodayAt(7), Description = "2" }, f.UserId);
        Assert.Empty(await f.Results(flag)); // the condition stopped holding: the value is retracted
    }

    [Fact]
    public async Task Conditional_TextSource_UsesTheLatestEntry_AndWritesAnOption()
    {
        var f = CreateFixture(nameof(Conditional_TextSource_UsesTheLatestEntry_AndWritesAnOption));
        var list = new TagOptionList { Name = "Breakfast kinds", UserId = f.UserId };
        f.Context.TagOptionLists.Add(list);
        await f.Context.SaveChangesAsync();
        f.Context.TagOptions.AddRange(
            new TagOption { OptionListId = list.Id, Value = "hotel", DisplayName = "Hotel breakfast" },
            new TagOption { OptionListId = list.Id, Value = "home", DisplayName = "At home" });
        var place = await f.AddTag("Place", inputTypeId: 2);
        var breakfast = await f.AddTag("Breakfast kind", inputTypeId: 2);
        var tracked = await f.Context.Tags.FindAsync(breakfast.Id);
        tracked!.OptionListId = list.Id;
        await f.Context.SaveChangesAsync();

        // The option is given by its display name and stored as its value.
        await CreateConditional(f, breakfast, place, ignoreZero: true,
            (TagRuleOperator.Equal, "HOTEL", "Hotel breakfast"),
            (TagRuleOperator.Otherwise, null, "home"));

        await f.Log(place, f.TodayAt(7), "home");
        await f.Log(place, f.TodayAt(20), "hotel"); // the latest entry decides
        Assert.Equal("hotel", Assert.Single(await f.Results(breakfast)).Description);

        var ruleId = (await f.Rules.GetAll(f.UserId)).Single().Id;
        await Assert.ThrowsAsync<ArgumentException>(() => f.Rules.Update(
            ruleId,
            new TagRuleRequest
            {
                Name = "Conditional",
                Template = TagRuleTemplate.Conditional,
                TargetTagId = breakfast.Id,
                Sources = [new() { SourceTagId = place.Id }],
                Cases = [new() { Operator = TagRuleOperator.IsLogged, ResultValue = "not an option" }]
            }, f.UserId));
    }

    [Fact]
    public async Task Conditional_YesNoSource_IsYesAndIsNo()
    {
        var f = CreateFixture(nameof(Conditional_YesNoSource_IsYesAndIsNo));
        var alcohol = await f.AddTag("Alcohol", inputTypeId: 3);
        var note = await f.AddTag("Evening", inputTypeId: 2);

        // "is no" cannot match while zero values ("no" entries) are ignored.
        await Assert.ThrowsAsync<ArgumentException>(() => CreateConditional(f, note, alcohol, ignoreZero: true,
            (TagRuleOperator.IsNo, null, "sober")));

        await CreateConditional(f, note, alcohol, ignoreZero: false,
            (TagRuleOperator.IsYes, null, "drank"),
            (TagRuleOperator.IsNo, null, "sober"));
        await f.Log(alcohol, f.TodayAt(21), "false");
        Assert.Equal("sober", Assert.Single(await f.Results(note)).Description);
    }

    [Fact]
    public async Task Conditional_Validation_RejectsCasesThatCannotWork()
    {
        var f = CreateFixture(nameof(Conditional_Validation_RejectsCasesThatCannotWork));
        var text = await f.AddTag("Text", inputTypeId: 2);
        var number = await f.AddTag("Number");
        var other = await f.AddTag("Other");
        var target = await f.AddTag("Target", inputTypeId: 1);
        var date = await f.AddTag("Date", inputTypeId: 4);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateConditional(f, target, number, true,
            (TagRuleOperator.Otherwise, null, "1"), (TagRuleOperator.Greater, "2", "2")));          // otherwise not last
        await Assert.ThrowsAsync<ArgumentException>(() => CreateConditional(f, target, text, true,
            (TagRuleOperator.Greater, "2", "1")));                                                 // > needs a number source
        await Assert.ThrowsAsync<ArgumentException>(() => CreateConditional(f, target, number, true,
            (TagRuleOperator.Equal, "abc", "1")));                                                 // operand must be a number
        await Assert.ThrowsAsync<ArgumentException>(() => CreateConditional(f, target, number, true,
            (TagRuleOperator.IsLogged, null, "lots")));                                            // number target needs a number
        await Assert.ThrowsAsync<ArgumentException>(() => CreateConditional(f, date, number, true,
            (TagRuleOperator.IsLogged, null, "2026-01-01")));                                      // date is not a result type
        await Assert.ThrowsAsync<ArgumentException>(() => f.Rules.Create(new TagRuleRequest
        {
            Name = "Two sources",
            Template = TagRuleTemplate.Conditional,
            TargetTagId = target.Id,
            Sources = [new() { SourceTagId = number.Id }, new() { SourceTagId = other.Id }],
            Cases = [new() { Operator = TagRuleOperator.IsLogged, ResultValue = "1" }]
        }, f.UserId));
    }

    [Fact]
    public async Task OnSqlite_ConditionalRule_SurvivesBackupRoundTrip()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var f = CreateSqliteFixture(connection);
        var coffees = await f.AddTag("Coffees");
        var caffeine = await f.AddTag("Caffeine", inputTypeId: 2);
        await CreateConditional(f, caffeine, coffees, ignoreZero: true,
            (TagRuleOperator.GreaterOrEqual, "4", "Over limit"),
            (TagRuleOperator.Otherwise, null, "Fine"));
        await f.Log(coffees, f.TodayAt(8), "5");

        var backups = new BackupService(f.Context, NullLogger<BackupService>.Instance, new TagRuleEngine(f.Context));
        var export = await backups.ExportDataAsync(f.UserId);
        Assert.Equal(2, Assert.Single(export.TagRules).Cases.Count);

        await backups.ClearDataAsync(f.UserId);
        var imported = await backups.ImportDataAsync(export, clearExistingData: false, f.UserId);
        Assert.True(imported.Success, string.Join("; ", imported.Errors) + string.Join("; ", imported.Warnings));

        var rule = await f.Context.TagRules.Include(r => r.Cases).SingleAsync();
        Assert.Equal([TagRuleOperator.GreaterOrEqual, TagRuleOperator.Otherwise], rule.Cases.OrderBy(c => c.SortOrder).Select(c => c.Operator));
        var restored = await f.Context.Tags.SingleAsync(t => t.TagName == "Caffeine" && t.UserId == f.UserId);
        Assert.Equal("Over limit", Assert.Single(await f.Results(restored)).Description);
    }

    [Fact]
    public async Task Update_WithoutATemplate_KeepsTheStoredTemplateAndCases()
    {
        // A client that predates templates sends name, sources and switches only.
        var f = CreateFixture(nameof(Update_WithoutATemplate_KeepsTheStoredTemplateAndCases));
        var coffees = await f.AddTag("Coffees");
        var level = await f.AddTag("Level", inputTypeId: 2);
        var average = await f.AddTag("Average");
        var conditional = await CreateConditional(f, level, coffees, ignoreZero: true,
            (TagRuleOperator.GreaterOrEqual, "4", "High"),
            (TagRuleOperator.Otherwise, null, "Low"));
        var aggregate = await CreateAggregate(f, average, TagRuleAggregateKind.Average, ignoreZero: true, coffees);

        await f.Rules.Update(conditional.Id, new TagRuleRequest
        {
            Name = "Renamed",
            TargetTagId = level.Id,
            IsEnabled = false,
            Sources = [new() { SourceTagId = coffees.Id, Factor = 1 }]
        }, f.UserId);
        await f.Rules.Update(aggregate.Id, new TagRuleRequest
        {
            Name = "Average",
            TargetTagId = average.Id,
            Sources = [new() { SourceTagId = coffees.Id, Factor = 1 }]
        }, f.UserId);

        var rules = await f.Rules.GetAll(f.UserId);
        var savedConditional = rules.Single(r => r.Id == conditional.Id);
        Assert.Equal(("Renamed", "Conditional", false), (savedConditional.Name, savedConditional.Template, savedConditional.IsEnabled));
        Assert.Equal(["GreaterOrEqual", "Otherwise"], savedConditional.Cases.Select(c => c.Operator));
        var savedAggregate = rules.Single(r => r.Id == aggregate.Id);
        Assert.Equal(("Aggregate", "Average"), (savedAggregate.Template, savedAggregate.AggregateKind));
    }

    [Fact]
    public async Task Conditional_ScaleSource_ComparesTheDayAverage()
    {
        var f = CreateFixture(nameof(Conditional_ScaleSource_ComparesTheDayAverage));
        var mood = await f.AddTag("Mood", inputTypeId: 10); // score 0-5
        var day = await f.AddTag("Day", inputTypeId: 2);
        await CreateConditional(f, day, mood, ignoreZero: true,
            (TagRuleOperator.GreaterOrEqual, "4", "good"),
            (TagRuleOperator.Otherwise, null, "meh"));

        await f.Log(mood, f.TodayAt(8), "3");
        await f.Log(mood, f.TodayAt(20), "4");
        Assert.Equal("meh", Assert.Single(await f.Results(day)).Description); // average 3.5, not the total 7

        await f.Log(mood, f.TodayAt(21), "5");
        Assert.Equal("good", Assert.Single(await f.Results(day)).Description); // average 4
    }

    [Fact]
    public async Task Conditional_DecimalDayTotal_MatchesDespiteBinaryRounding()
    {
        var f = CreateFixture(nameof(Conditional_DecimalDayTotal_MatchesDespiteBinaryRounding));
        var water = await f.AddTag("Water", inputTypeId: 6);
        var flag = await f.AddTag("Amount", inputTypeId: 2);
        await CreateConditional(f, flag, water, ignoreZero: true,
            (TagRuleOperator.Equal, "0.3", "exact"),
            (TagRuleOperator.Greater, "0.3", "over"),
            (TagRuleOperator.Otherwise, null, "under"));

        await f.Log(water, f.TodayAt(8), "0.1");
        await f.Log(water, f.TodayAt(9), "0.2"); // 0.30000000000000004 as a double

        Assert.Equal("exact", Assert.Single(await f.Results(flag)).Description);
    }

    [Fact]
    public async Task Conditional_OptionListOnANumberTag_ComparesTheLatestOption()
    {
        var f = CreateFixture(nameof(Conditional_OptionListOnANumberTag_ComparesTheLatestOption));
        var list = new TagOptionList { Name = "Levels", UserId = f.UserId };
        f.Context.TagOptionLists.Add(list);
        await f.Context.SaveChangesAsync();
        f.Context.TagOptions.AddRange(
            new TagOption { OptionListId = list.Id, Value = "1", DisplayName = "Low" },
            new TagOption { OptionListId = list.Id, Value = "3", DisplayName = "High" });
        var level = await f.AddTag("Level");
        (await f.Context.Tags.FindAsync(level.Id))!.OptionListId = list.Id;
        await f.Context.SaveChangesAsync();
        var label = await f.AddTag("Label", inputTypeId: 2);

        // The compared option is given by its display name and stored as its value.
        var rule = await CreateConditional(f, label, level, ignoreZero: true,
            (TagRuleOperator.Equal, "High", "high day"),
            (TagRuleOperator.Otherwise, null, "other"));
        Assert.Equal("3", rule.Cases[0].Operand);

        await f.Log(level, f.TodayAt(8), "3");
        Assert.Equal("high day", Assert.Single(await f.Results(label)).Description);

        await f.Log(level, f.TodayAt(9), "1"); // the latest option decides; a day total would be 4
        Assert.Equal("other", Assert.Single(await f.Results(label)).Description);
    }

    [Fact]
    public async Task YesNoSource_AcceptsYesNoSpellings_AndNoIsASkipMarker()
    {
        var f = CreateFixture(nameof(YesNoSource_AcceptsYesNoSpellings_AndNoIsASkipMarker));
        var alcohol = await f.AddTag("Alcohol", inputTypeId: 3);
        var evening = await f.AddTag("Evening", inputTypeId: 2);
        var drinks = await f.AddTag("Drinks");
        await CreateConditional(f, evening, alcohol, ignoreZero: true, (TagRuleOperator.IsYes, null, "drank"));
        await CreateAggregate(f, drinks, TagRuleAggregateKind.Count, ignoreZero: true, alcohol);

        await f.Log(alcohol, f.TodayAt(20), "yes");
        await f.Log(alcohol, f.TodayAt(22), "no");

        Assert.Equal("1", Assert.Single(await f.Results(drinks)).Description); // "no" is left out
        Assert.Equal("drank", Assert.Single(await f.Results(evening)).Description);
    }

    [Fact]
    public async Task Conditional_EntriesAtTheSameMoment_ResolveByTheLaterEntry()
    {
        var f = CreateFixture(nameof(Conditional_EntriesAtTheSameMoment_ResolveByTheLaterEntry));
        var place = await f.AddTag("Place", inputTypeId: 2);
        var where = await f.AddTag("Where", inputTypeId: 2);
        await CreateConditional(f, where, place, ignoreZero: true,
            (TagRuleOperator.Equal, "b", "second"),
            (TagRuleOperator.Otherwise, null, "first"));

        await f.Log(place, f.TodayAt(8), "a");
        await f.Log(place, f.TodayAt(8), "b");

        Assert.Equal("second", Assert.Single(await f.Results(where)).Description);
    }

    [Fact]
    public async Task Validation_RejectsUnknownValues_AndEqualsZeroWhileZerosAreIgnored()
    {
        var f = CreateFixture(nameof(Validation_RejectsUnknownValues_AndEqualsZeroWhileZerosAreIgnored));
        var number = await f.AddTag("Number");
        var target = await f.AddTag("Target", inputTypeId: 2);

        await Assert.ThrowsAsync<ArgumentException>(() => f.Rules.Create(new TagRuleRequest
        {
            Name = "Unknown template",
            Template = (TagRuleTemplate)3,
            TargetTagId = target.Id,
            Sources = [new() { SourceTagId = number.Id }]
        }, f.UserId));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Rules.Create(new TagRuleRequest
        {
            Name = "Unknown kind",
            Template = TagRuleTemplate.Aggregate,
            AggregateKind = (TagRuleAggregateKind)9,
            TargetTagId = target.Id,
            Sources = [new() { SourceTagId = number.Id }]
        }, f.UserId));
        await Assert.ThrowsAsync<ArgumentException>(() => CreateConditional(f, target, number, ignoreZero: false,
            ((TagRuleOperator)42, "1", "x")));
        await Assert.ThrowsAsync<ArgumentException>(() => CreateConditional(f, target, number, ignoreZero: true,
            (TagRuleOperator.Equal, "0", "none")));

        var counted = await CreateConditional(f, target, number, ignoreZero: false, (TagRuleOperator.Equal, "0", "none"));
        Assert.Equal("Equal", Assert.Single(counted.Cases).Operator);
    }

    [Fact]
    public async Task OnSqlite_BackupImport_ChecksCasesAgainstTheTagsTheyNameNow()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var f = CreateSqliteFixture(connection);
        await f.AddTag("Coffees");
        await f.AddTag("Too much", inputTypeId: 3);
        await f.AddTag("Over limit", inputTypeId: 3);
        var backups = new BackupService(f.Context, NullLogger<BackupService>.Instance, new TagRuleEngine(f.Context));

        TagRuleBackup Rule(string name, string target, string result) => new()
        {
            Name = name,
            Template = TagRuleTemplate.Conditional,
            TargetTagName = target,
            IgnoreZero = true,
            IsEnabled = true,
            EffectiveFrom = f.Today,
            Sources = [new() { SourceTagName = "Coffees", Factor = 1 }],
            Cases = [new() { Operator = TagRuleOperator.GreaterOrEqual, Operand = "4", ResultValue = result }]
        };

        var result = await backups.ImportDataAsync(new BackupData
        {
            Metadata = new BackupMetadata(),
            TagRules = [Rule("Bad", "Too much", "lots"), Rule("Good", "Over limit", "Yes")]
        }, clearExistingData: false, f.UserId);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal(1, result.Statistics.TagRulesSkipped);
        Assert.Contains(result.Warnings, w => w.Contains("'Bad'") && w.Contains("yes or no"));
        var restored = await f.Context.TagRules.Include(r => r.Cases).SingleAsync();
        Assert.Equal(("Good", "true"), (restored.Name, Assert.Single(restored.Cases).ResultValue));
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
