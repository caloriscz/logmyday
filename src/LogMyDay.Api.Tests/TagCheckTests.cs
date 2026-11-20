using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.Tests;

/// <summary>Tag Activity Relations checks on a real SQLite database: what each kind of check
/// finds in the Rule review, validation, delete guards and backups.</summary>
public sealed class TagCheckTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LogMyDayDbContext _context;
    private readonly TagCheckService _checks;
    private readonly RecordTypeService _types;
    private readonly RecordService _records;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);

    public TagCheckTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _context.Users.Add(new User { Id = _userId, Email = "t@t", PasswordHash = "x", TimeZone = "UTC", Culture = "en-US" });
        _context.SaveChanges();

        var eventLog = new EventLogService(_context, NullLogger<EventLogService>.Instance);
        _checks = new TagCheckService(_context, eventLog);
        _types = new RecordTypeService(_context, eventLog);
        _records = new RecordService(_context, eventLog, new TagRuleEngine(_context));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private DateOnly Day(int daysAgo) => _today.AddDays(-daysAgo);

    private async Task<Tag> AddTag(string name, int inputTypeId, int? optionListId = null, Guid? owner = null)
    {
        var tag = new Tag
        {
            TagName = name,
            InputTypeId = inputTypeId,
            OptionListId = optionListId,
            TimeGranularity = TimeGranularity.Daily,
            UserId = owner ?? _userId
        };
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return tag;
    }

    // Entries go straight to the table: the review reads what is stored, however it got there.
    private async Task Log(Tag tag, int daysAgo, string value, int hour = 9)
    {
        _context.Activities.Add(new Activity
        {
            UserId = _userId,
            TagId = tag.Id,
            DateStarted = Day(daysAgo).ToDateTime(new TimeOnly(hour, 0)),
            DateCreated = DateTime.UtcNow,
            Description = value
        });
        await _context.SaveChangesAsync();
    }

    private Task<TagCheckResponse> Check(Tag ifTag, TagRuleOperator ifOperator, string? ifOperand,
        Tag thenTag, TagRuleOperator thenOperator, string? thenOperand = null, string name = "Check", bool enabled = true,
        TagCheckSeverity severity = TagCheckSeverity.Flag) =>
        _checks.Create(new TagCheckRequest
        {
            Name = name,
            IsEnabled = enabled,
            Severity = severity,
            IfTagId = ifTag.Id,
            IfOperator = ifOperator,
            IfOperand = ifOperand,
            ThenTagId = thenTag.Id,
            ThenOperator = thenOperator,
            ThenOperand = thenOperand
        }, _userId);

    private async Task<List<TagCheckReviewItem>> Review(int fromDaysAgo = 10)
    {
        return (await _checks.Review(Day(fromDaysAgo), _today, _userId)).Items;
    }

    private async Task<TagOptionList> OptionList(string name, params (string Value, string Display)[] options)
    {
        var list = new TagOptionList { Name = name, UserId = _userId };
        _context.TagOptionLists.Add(list);
        await _context.SaveChangesAsync();
        _context.TagOptions.AddRange(options.Select(o => new TagOption { OptionListId = list.Id, Value = o.Value, DisplayName = o.Display }));
        await _context.SaveChangesAsync();

        return list;
    }

    [Fact]
    public async Task IfThen_ShowsDaysWhoseValueContradicts_NotDaysWithoutTheSecondTag()
    {
        var coffees = await AddTag("Coffees", InputTypeIds.Integer);
        var late = await AddTag("Late coffee", InputTypeIds.Boolean);
        var check = await Check(coffees, TagRuleOperator.Equal, "4", late, TagRuleOperator.IsNo, name: "Four coffees");
        Assert.Equal("IfThen", check.Kind);

        await Log(coffees, 5, "2", hour: 8);
        await Log(coffees, 5, "2", hour: 12); // the day total is 4
        await Log(late, 5, "true");
        await Log(coffees, 4, "4");
        await Log(late, 4, "false");
        await Log(coffees, 3, "4"); // no late coffee logged: nothing contradicts
        await Log(coffees, 2, "3");
        await Log(late, 2, "true"); // condition does not hold

        var item = Assert.Single(await Review());
        Assert.Equal(Day(5), item.Date);
        Assert.Equal("Four coffees", item.CheckName);
        Assert.Equal(("4", "true"), (item.IfValue, item.ThenValue));
        Assert.False(item.IsPending);
        Assert.Equal("Flag", item.Severity);
    }

    [Fact]
    public async Task NotOnTheSameDay_DoesNotCountSkipMarkersAsLogged()
    {
        var fasting = await AddTag("Fasting", InputTypeIds.Boolean);
        var breakfast = await AddTag("Breakfast", InputTypeIds.Integer);
        var check = await Check(fasting, TagRuleOperator.IsLogged, null, breakfast, TagRuleOperator.IsNotLogged);
        Assert.Equal("NotOnTheSameDay", check.Kind);

        await Log(fasting, 3, "true");
        await Log(breakfast, 3, "0"); // a skipped breakfast reminder
        await Log(fasting, 2, "true");
        await Log(breakfast, 2, "1");
        await Log(fasting, 1, "false"); // not fasting
        await Log(breakfast, 1, "1");

        var item = Assert.Single(await Review());
        Assert.Equal(Day(2), item.Date);
        Assert.Equal("1", item.ThenValue);
    }

    [Fact]
    public async Task Requires_PastDaysWithoutTheSecondTagBreak_TodayIsPending()
    {
        var shake = await AddTag("Shake", InputTypeIds.Integer);
        var exercise = await AddTag("Exercise", InputTypeIds.Boolean);
        var check = await Check(shake, TagRuleOperator.IsLogged, null, exercise, TagRuleOperator.IsLogged);
        Assert.Equal("Requires", check.Kind);

        await Log(shake, 3, "1");
        await Log(exercise, 3, "false"); // "no" did not happen
        await Log(shake, 2, "1");
        await Log(shake, 1, "1");
        await Log(exercise, 1, "true");
        await Log(shake, 0, "1");

        var items = await Review();

        Assert.Equal([Day(0), Day(2), Day(3)], items.Select(i => i.Date));
        Assert.Equal([true, false, false], items.Select(i => i.IsPending));
        Assert.Equal([null, null, "false"], items.Select(i => i.ThenValue));
    }

    [Fact]
    public async Task ScaleTextAndOptionTags_AreComparedByTheirDayValue()
    {
        var places = await OptionList("Places", ("hotel", "Hotel breakfast"), ("home", "At home"));
        var mood = await AddTag("Mood", InputTypeIds.Score);
        var note = await AddTag("Note", InputTypeIds.String);
        var place = await AddTag("Place", InputTypeIds.String, places.Id);
        await Check(mood, TagRuleOperator.GreaterOrEqual, "4", note, TagRuleOperator.Equal, "good", name: "Good mood");
        var byPlace = await Check(place, TagRuleOperator.Equal, "Hotel breakfast", mood, TagRuleOperator.GreaterOrEqual, "3", name: "Hotel mood");
        Assert.Equal("hotel", byPlace.IfOperand); // an option is stored by its value

        await Log(mood, 2, "5", hour: 8);
        await Log(mood, 2, "3", hour: 20); // the day average is 4
        await Log(note, 2, "bad", hour: 8);
        await Log(note, 2, "GOOD", hour: 21); // the latest entry counts, case-insensitively
        await Log(mood, 1, "4");
        await Log(note, 1, "bad");
        await Log(place, 3, "hotel");
        await Log(mood, 3, "2");

        var items = await Review();

        Assert.Equal([(Day(1), "Good mood"), (Day(3), "Hotel mood")], items.Select(i => (i.Date, i.CheckName)));
        Assert.Equal(("4", "bad"), (items[0].IfValue, items[0].ThenValue));
        Assert.Equal(("hotel", "2"), (items[1].IfValue, items[1].ThenValue));
    }

    [Fact]
    public async Task ARecordField_CanBeChecked_AndStaysInItsTypeWhileChecked()
    {
        var type = await _types.Create(new RecordTypeRequest
        {
            Name = "Blood pressure",
            Fields = [new() { Name = "Systolic", TypeId = InputTypeIds.Integer }, new() { Name = "Pulse", TypeId = InputTypeIds.Integer }]
        }, _userId);
        var medication = await AddTag("Medication", InputTypeIds.Boolean);
        var systolic = await _context.Tags.SingleAsync(t => t.Id == type.Fields[0].TagId);
        await Check(systolic, TagRuleOperator.Greater, "140", medication, TagRuleOperator.IsLogged, name: "High pressure");

        foreach (var (daysAgo, value) in new[] { (2, "150"), (1, "120") })
        {
            await _records.Create(new RecordRequest
            {
                RecordTypeId = type.Id,
                DateStarted = Day(daysAgo).ToDateTime(new TimeOnly(7, 0)),
                Values = [new() { TagId = type.Fields[0].TagId, Value = value }]
            }, _userId);
        }

        var item = Assert.Single(await Review());
        Assert.Equal((Day(2), "150"), (item.Date, item.IfValue));

        // A record type holds no values, and a checked field cannot leave it.
        var recordType = await _context.Tags.SingleAsync(t => t.Id == type.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => Check(recordType, TagRuleOperator.IsLogged, null, medication, TagRuleOperator.IsLogged));
        var removal = await Assert.ThrowsAsync<InvalidOperationException>(() => _types.Update(type.Id, new RecordTypeRequest
        {
            Name = "Blood pressure",
            Fields = [new() { TagId = type.Fields[1].TagId, Name = "Pulse", TypeId = InputTypeIds.Integer }]
        }, _userId));
        Assert.Contains("'High pressure'", removal.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _types.Delete(type.Id, _userId));
    }

    [Fact]
    public async Task Validation_RefusesChecksThatCannotWork()
    {
        var places = await OptionList("Places", ("hotel", "Hotel breakfast"));
        var number = await AddTag("Number", InputTypeIds.Integer);
        var yesNo = await AddTag("Yes or no", InputTypeIds.Boolean);
        var text = await AddTag("Text", InputTypeIds.String);
        var place = await AddTag("Place", InputTypeIds.String, places.Id);
        var foreign = await AddTag("Foreign", InputTypeIds.Integer, owner: Guid.NewGuid());

        async Task Refused(Tag ifTag, TagRuleOperator ifOperator, string? ifOperand, Tag thenTag, TagRuleOperator thenOperator, string? thenOperand, string expected)
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => Check(ifTag, ifOperator, ifOperand, thenTag, thenOperator, thenOperand));
            Assert.Contains(expected, ex.Message);
        }

        await Refused(number, TagRuleOperator.IsLogged, null, number, TagRuleOperator.IsNotLogged, null, "two different tags");
        await Refused(number, TagRuleOperator.Otherwise, null, yesNo, TagRuleOperator.IsLogged, null, "only for rules");
        await Refused(number, TagRuleOperator.IsNotLogged, null, yesNo, TagRuleOperator.IsLogged, null, "can only be expected");
        await Refused(number, TagRuleOperator.IsYes, null, yesNo, TagRuleOperator.IsLogged, null, "need a yes/no tag");
        await Refused(number, TagRuleOperator.IsLogged, null, text, TagRuleOperator.Greater, "3", "greater and less than");
        await Refused(number, TagRuleOperator.Equal, "four", yesNo, TagRuleOperator.IsLogged, null, "enter a number");
        await Refused(number, TagRuleOperator.IsLogged, null, place, TagRuleOperator.Equal, "Beach", "not an option");
        await Refused(yesNo, TagRuleOperator.Equal, "true", number, TagRuleOperator.IsLogged, null, "use \"is yes\" or \"is no\"");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Check(foreign, TagRuleOperator.IsLogged, null, number, TagRuleOperator.IsLogged));
        await Assert.ThrowsAsync<ArgumentException>(() => _checks.Create(new TagCheckRequest
        {
            Name = " ",
            IfTagId = number.Id,
            ThenTagId = yesNo.Id
        }, _userId));
        await Assert.ThrowsAsync<ArgumentException>(() => _checks.Create(new TagCheckRequest
        {
            Name = "Unknown severity",
            Severity = (TagCheckSeverity)9,
            IfTagId = number.Id,
            ThenTagId = yesNo.Id
        }, _userId));
        Assert.False(await _context.TagChecks.AnyAsync());
    }

    [Fact]
    public async Task Review_KeepsToAYearUpToToday_AndSkipsPausedChecks()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _checks.Review(Day(1), Day(2), _userId));
        await Assert.ThrowsAsync<ArgumentException>(() => _checks.Review(Day(1), _today.AddDays(1), _userId));
        await Assert.ThrowsAsync<ArgumentException>(() => _checks.Review(Day(366), _today, _userId));
        var year = await _checks.Review(Day(365), _today, _userId); // 366 days, the most
        Assert.Equal((Day(365), _today), (year.From, year.To));

        var defaults = await _checks.Review(null, null, _userId);
        Assert.Equal((Day(29), _today), (defaults.From, defaults.To));

        var a = await AddTag("A", InputTypeIds.Integer);
        var b = await AddTag("B", InputTypeIds.Integer);
        await Check(a, TagRuleOperator.IsLogged, null, b, TagRuleOperator.IsNotLogged, enabled: false);
        await Log(a, 1, "1");
        await Log(b, 1, "1");

        var paused = await _checks.Review(null, null, _userId);
        Assert.Equal(0, paused.CheckCount);
        Assert.Empty(paused.Items);
    }

    [Fact]
    public async Task UpdateAndDelete_ChangeOnlyTheCheck()
    {
        var a = await AddTag("A", InputTypeIds.Integer);
        var b = await AddTag("B", InputTypeIds.Integer);
        var created = await Check(a, TagRuleOperator.IsLogged, null, b, TagRuleOperator.IsNotLogged);
        await Log(a, 1, "1");

        var updated = await _checks.Update(created.Id, new TagCheckRequest
        {
            Name = "A needs B",
            Severity = TagCheckSeverity.Warn,
            IfTagId = a.Id,
            IfOperator = TagRuleOperator.IsLogged,
            ThenTagId = b.Id,
            ThenOperator = TagRuleOperator.IsLogged
        }, _userId);

        Assert.Equal(("A needs B", "Requires", "Warn"), (updated.Name, updated.Kind, updated.Severity));
        Assert.Equal("Warn", Assert.Single(await Review()).Severity);

        await _checks.Delete(created.Id, _userId);
        Assert.False(await _context.TagChecks.AnyAsync());
        Assert.Equal(1, await _context.Activities.CountAsync());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _checks.GetById(created.Id, _userId));
    }

    [Fact]
    public async Task TagDelete_IsRefusedWhileACheckUsesTheTag()
    {
        var a = await AddTag("A", InputTypeIds.Integer);
        var b = await AddTag("B", InputTypeIds.Integer);
        var check = await Check(a, TagRuleOperator.IsLogged, null, b, TagRuleOperator.IsNotLogged, name: "Not both");
        var tags = new TagService(_context, new TagRepository(_context), NullLogger<TagService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tags.Delete(b.Id, _userId));
        Assert.Contains("'Not both'", ex.Message);

        await _checks.Delete(check.Id, _userId);
        await tags.Delete(b.Id, _userId);
        Assert.False(await _context.Tags.AnyAsync(t => t.Id == b.Id));
    }

    [Fact]
    public async Task TagUpdate_KeepsTheTypeWhileACheckUsesTheTag()
    {
        var a = await AddTag("A", InputTypeIds.Integer);
        var b = await AddTag("B", InputTypeIds.Integer);
        await Check(a, TagRuleOperator.Greater, "140", b, TagRuleOperator.IsLogged, name: "High needs B");
        var tags = new TagService(_context, new TagRepository(_context), NullLogger<TagService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tags.Update(a.Id, new TagRequest { Tag = "A", TypeId = InputTypeIds.String }, _userId));
        Assert.Contains("'High needs B'", ex.Message);
        Assert.Equal(InputTypeIds.Integer, (await _context.Tags.AsNoTracking().SingleAsync(t => t.Id == a.Id)).InputTypeId);
    }

    [Fact]
    public async Task Rules_RefuseIsNotLogged()
    {
        var a = await AddTag("A", InputTypeIds.Integer);
        var result = await AddTag("Result", InputTypeIds.String);
        var eventLog = new EventLogService(_context, NullLogger<EventLogService>.Instance);
        var rules = new TagRuleService(_context, new TagRuleEngine(_context), eventLog);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => rules.Create(new TagRuleRequest
        {
            Name = "Missing",
            Template = TagRuleTemplate.Conditional,
            TargetTagId = result.Id,
            Sources = [new() { SourceTagId = a.Id }],
            Cases = [new() { Operator = TagRuleOperator.IsNotLogged, ResultValue = "none" }]
        }, _userId));
        Assert.Contains("only for checks", ex.Message);
    }

    [Fact]
    public async Task BackupRoundTrip_KeepsTheChecks_AndSkipsOneWhoseTagIsMissing()
    {
        var places = await OptionList("Places", ("hotel", "Hotel breakfast"));
        var place = await AddTag("Place", InputTypeIds.String, places.Id);
        var mood = await AddTag("Mood", InputTypeIds.Score);
        await Check(place, TagRuleOperator.Equal, "hotel", mood, TagRuleOperator.GreaterOrEqual, "3", name: "Hotel mood");
        var backups = new BackupService(_context, NullLogger<BackupService>.Instance, new TagRuleEngine(_context));

        var export = await backups.ExportDataAsync(_userId);
        var exported = Assert.Single(export.TagChecks);
        Assert.Equal(("Place", "Mood", "hotel", "3"), (exported.IfTagName, exported.ThenTagName, exported.IfOperand, exported.ThenOperand));
        Assert.Equal(1, export.Metadata.TotalTagChecks);

        await backups.ClearDataAsync(_userId);
        Assert.False(await _context.TagChecks.AnyAsync());

        var imported = await backups.ImportDataAsync(export, clearExistingData: false, _userId);
        Assert.True(imported.Success, string.Join("; ", imported.Errors));
        Assert.Equal(1, imported.Statistics.TagChecksImported);
        var restored = Assert.Single(await _checks.GetAll(_userId));
        Assert.Equal(("Hotel mood", "Equal", "hotel", "GreaterOrEqual", "3"),
            (restored.Name, restored.IfOperator, restored.IfOperand, restored.ThenOperator, restored.ThenOperand));

        // Importing again does not double the check.
        var again = await backups.ImportDataAsync(export, clearExistingData: false, _userId);
        Assert.Equal((0, 1), (again.Statistics.TagChecksImported, again.Statistics.TagChecksSkipped));
        Assert.Single(await _checks.GetAll(_userId));

        // The secure backup carries the checks too.
        var secure = await backups.CreateSecureBackup(_userId);
        Assert.Single(secure.TagChecks);
        await backups.ClearUserData(_userId);
        Assert.False(await _context.TagChecks.AnyAsync());
        var restoredSecure = await backups.RestoreSecureBackup(secure, _userId);
        Assert.True(restoredSecure.Success, string.Join("; ", restoredSecure.Errors));
        Assert.Equal(1, restoredSecure.Statistics.TagChecksImported);

        // A check naming a tag the backup does not have is skipped with a warning.
        await backups.ClearDataAsync(_userId);
        export.TagChecks[0].ThenTagName = "Missing";
        var partial = await backups.ImportDataAsync(export, clearExistingData: false, _userId);
        Assert.Equal((0, 1), (partial.Statistics.TagChecksImported, partial.Statistics.TagChecksSkipped));
        Assert.Contains(partial.Warnings, w => w.Contains("'Hotel mood'") && w.Contains("'Missing'"));
    }

    // What a save of one entry returns: the warnings for its tag and day.
    private Task<List<TagCheckWarning>?> WarningsAfter(Tag tag, int daysAgo) =>
        _checks.WarningsAfterSave(_userId, new ActivityResponse { PrimaryTagId = tag.Id, DateStarted = Day(daysAgo).ToDateTime(new TimeOnly(9, 0)) });

    [Fact]
    public async Task ASave_WarnsOnlyAboutTheWarnChecksItBreaks()
    {
        var fasting = await AddTag("Fasting", InputTypeIds.Boolean);
        var breakfast = await AddTag("Breakfast", InputTypeIds.Integer);
        await Check(fasting, TagRuleOperator.IsLogged, null, breakfast, TagRuleOperator.IsNotLogged, name: "Fast", severity: TagCheckSeverity.Warn);
        await Check(fasting, TagRuleOperator.IsLogged, null, breakfast, TagRuleOperator.IsNotLogged, name: "Fast (flag)");
        await Check(fasting, TagRuleOperator.IsLogged, null, breakfast, TagRuleOperator.IsNotLogged, name: "Fast (paused)", enabled: false, severity: TagCheckSeverity.Warn);

        await Log(fasting, 1, "true");
        Assert.Null(await WarningsAfter(fasting, 1));

        // Breakfast on the fasting day breaks all three; only the active Warn check warns, on
        // either tag's save.
        await Log(breakfast, 1, "1");
        var warning = Assert.Single((await WarningsAfter(breakfast, 1))!);
        Assert.Equal(("Fast", "Fasting and Breakfast are not on the same day", "Fasting: yes · Breakfast: 1"),
            (warning.CheckName, warning.Sentence, warning.Found));
        Assert.Single((await WarningsAfter(fasting, 1))!);

        // A skipped breakfast (0) does not count as logged, so it does not warn.
        await Log(fasting, 2, "true");
        await Log(breakfast, 2, "0");
        Assert.Null(await WarningsAfter(breakfast, 2));

        // Another tag's save does not look at these checks.
        var water = await AddTag("Water", InputTypeIds.Integer);
        Assert.Null(await WarningsAfter(water, 1));
    }

    [Fact]
    public async Task Requires_NeverWarnsAboutToday_ButDoesAboutAPastDay()
    {
        var shake = await AddTag("Shake", InputTypeIds.Boolean);
        var exercise = await AddTag("Exercise", InputTypeIds.Integer);
        await Check(shake, TagRuleOperator.IsLogged, null, exercise, TagRuleOperator.IsLogged, name: "Shake needs exercise", severity: TagCheckSeverity.Warn);

        await Log(shake, 0, "true");
        Assert.Null(await WarningsAfter(shake, 0));

        await Log(shake, 1, "true");
        var warning = Assert.Single((await WarningsAfter(shake, 1))!);
        Assert.Equal(("Shake requires Exercise on the same day", "Shake: yes · Exercise: not logged"), (warning.Sentence, warning.Found));
    }

    [Fact]
    public async Task ACheckOnAComputedTag_WarnsAfterASourceIsSaved()
    {
        var espresso = await AddTag("Espresso", InputTypeIds.Integer);
        var total = await AddTag("Caffeine drinks", InputTypeIds.Integer);
        var water = await AddTag("Water", InputTypeIds.Integer);
        var eventLog = new EventLogService(_context, NullLogger<EventLogService>.Instance);
        var rules = new TagRuleService(_context, new TagRuleEngine(_context), eventLog);
        await rules.Create(new TagRuleRequest
        {
            Name = "Caffeine",
            TargetTagId = total.Id,
            Sources = [new() { SourceTagId = espresso.Id, Factor = 1 }]
        }, _userId);
        await Check(total, TagRuleOperator.GreaterOrEqual, "3", water, TagRuleOperator.GreaterOrEqual, "2", name: "Water with coffee", severity: TagCheckSeverity.Warn);

        await Log(water, 0, "1");
        await Log(espresso, 0, "2");
        await new TagRuleEngine(_context).OnSourcesChanged(_userId, [(espresso.Id, _today)]);
        Assert.Null(await WarningsAfter(espresso, 0));

        // The third espresso brings the computed total to 3 with only one glass of water.
        await Log(espresso, 0, "1", hour: 11);
        await new TagRuleEngine(_context).OnSourcesChanged(_userId, [(espresso.Id, _today)]);
        var warning = Assert.Single((await WarningsAfter(espresso, 0))!);
        Assert.Equal(("Water with coffee", "Caffeine drinks: 3 · Water: 1"), (warning.CheckName, warning.Found));
    }

    [Fact]
    public async Task ARecordSave_WarnsForAnyFieldOfItsType()
    {
        var type = await _types.Create(new RecordTypeRequest
        {
            Name = "Run",
            Fields = [new() { Name = "Distance", TypeId = InputTypeIds.Decimal }, new() { Name = "Note", TypeId = InputTypeIds.String }]
        }, _userId);
        var distance = await _context.Tags.SingleAsync(t => t.Id == type.Fields[0].TagId);
        var note = await _context.Tags.SingleAsync(t => t.Id == type.Fields[1].TagId);
        await Check(distance, TagRuleOperator.IsLogged, null, note, TagRuleOperator.IsLogged, name: "Runs have a note", severity: TagCheckSeverity.Warn);

        var record = await _records.Create(new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = Day(1).ToDateTime(new TimeOnly(7, 0)),
            Values = [new() { TagId = distance.Id, Value = "5" }]
        }, _userId);
        var warning = Assert.Single((await _checks.WarningsAfterSave(_userId, record))!);
        Assert.Equal("Runs have a note", warning.CheckName);

        var withNote = await _records.Update(record.Id, new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = record.DateStarted,
            Values = [new() { TagId = distance.Id, Value = "5" }, new() { TagId = note.Id, Value = "Easy" }]
        }, _userId);
        Assert.Null(await _checks.WarningsAfterSave(_userId, withNote));
    }

    [Fact]
    public void Responses_LeaveOutWarnings_WhenThereAreNone()
    {
        var options = LogMyDay.Shared.Serialization.JsonSerializationSettings.CreateDefault();

        Assert.DoesNotContain("warnings", System.Text.Json.JsonSerializer.Serialize(new ActivityResponse(), options));
        Assert.DoesNotContain("warnings", System.Text.Json.JsonSerializer.Serialize(new RecordResponse(), options));
        Assert.Contains("\"warnings\":[", System.Text.Json.JsonSerializer.Serialize(new ActivityResponse { Warnings = [new()] }, options));
    }
}
