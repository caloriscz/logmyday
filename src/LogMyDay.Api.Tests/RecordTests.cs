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

/// <summary>Records on a real SQLite database: the record row and its values are written in one
/// transaction, and the seeded input types are there for the fields.</summary>
public sealed class RecordTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LogMyDayDbContext _context;
    private readonly ActivityService _activities;
    private readonly TagRuleService _rules;
    private readonly RecordTypeService _types;
    private readonly RecordService _records;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);

    public RecordTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _context.Users.Add(new User { Id = _userId, Email = "t@t", PasswordHash = "x", TimeZone = "UTC", Culture = "en-US" });
        _context.SaveChanges();

        var engine = new TagRuleEngine(_context);
        var eventLog = new EventLogService(_context, NullLogger<EventLogService>.Instance);
        _activities = new ActivityService(_context, new ActivityRepository(_context), eventLog, new TagDayLockService(_context), engine);
        _rules = new TagRuleService(_context, engine, eventLog);
        _types = new RecordTypeService(_context, eventLog);
        _records = new RecordService(_context, eventLog, engine);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private DateTime At(int hour, int daysAgo = 0) => _today.AddDays(-daysAgo).ToDateTime(new TimeOnly(hour, 0));

    private static RecordFieldRequest Field(string name, int typeId = InputTypeIds.Integer) =>
        new() { Name = name, TypeId = typeId };

    private Task<RecordTypeResponse> BloodPressure(bool oncePerDay = false) =>
        _types.Create(new RecordTypeRequest
        {
            Name = "Blood pressure",
            OncePerDay = oncePerDay,
            Fields = [Field("Systolic"), Field("Diastolic"), Field("Pulse")]
        }, _userId);

    private Task<RecordResponse> Log(RecordTypeResponse type, DateTime when, params string?[] values) =>
        _records.Create(new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = when,
            Values = type.Fields.Zip(values, (f, v) => new RecordValueRequest { TagId = f.TagId, Value = v }).ToList()
        }, _userId);

    private async Task<List<Activity>> Values(int tagId)
    {
        _context.ChangeTracker.Clear();

        return await _context.Activities.Where(a => a.TagId == tagId).OrderBy(a => a.DateStarted).ThenBy(a => a.Id).ToListAsync();
    }

    private async Task<Tag> AddTag(string name, int inputTypeId = InputTypeIds.Decimal)
    {
        var tag = new Tag { TagName = name, InputTypeId = inputTypeId, TimeGranularity = TimeGranularity.Daily, UserId = _userId };
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return tag;
    }

    [Fact]
    public async Task CreatingARecordType_AddsAFieldTagPerField()
    {
        var group = new TagGroup { Name = "Health", UserId = _userId, DateCreated = DateTime.UtcNow };
        _context.TagGroups.Add(group);
        await _context.SaveChangesAsync();

        var type = await _types.Create(new RecordTypeRequest
        {
            Name = "Run",
            GroupId = group.Id,
            Fields = [Field("Distance", InputTypeIds.Decimal), Field("Heart rate"), Field("Note", InputTypeIds.String)]
        }, _userId);

        Assert.Equal(["Distance", "Heart rate", "Note"], type.Fields.Select(f => f.Name));
        Assert.Equal(["Run: Distance", "Run: Heart rate", "Run: Note"], type.Fields.Select(f => f.TagName));

        // The record type itself is a tag that holds no values: no input type, in the same group.
        _context.ChangeTracker.Clear();
        var typeTag = await _context.Tags.SingleAsync(t => t.Id == type.Id);
        Assert.True(typeTag.IsRecordType);
        Assert.Equal("Run", typeTag.TagName);
        Assert.Equal(group.Id, typeTag.GroupId);
        Assert.Null(typeTag.InputTypeId);
        Assert.True(typeTag.IsRepeatable);

        var tags = await _context.Tags.Where(t => t.RecordTypeId == type.Id).OrderBy(t => t.RecordFieldOrder).ToListAsync();
        Assert.Equal(3, tags.Count);
        Assert.All(tags, t => Assert.Equal(group.Id, t.GroupId));
        Assert.All(tags, t => Assert.Equal(TimeGranularity.Exact, t.TimeGranularity));
    }

    [Fact]
    public async Task ARecordType_IsLeftOutOfTagLists_AndTakesNoValuesOfItsOwn()
    {
        var type = await BloodPressure();
        var plain = await AddTag("Plain");
        var tags = new TagService(_context, new TagRepository(_context), NullLogger<TagService>.Instance);

        var all = await tags.GetAll(_userId);
        Assert.DoesNotContain(all, t => t.Id == type.Id);
        Assert.Contains(all, t => t.Id == plain.Id);
        Assert.Equal(3, all.Count(t => t.RecordTypeId == type.Id)); // its fields stay readable
        var page = await tags.GetPaged(1, 50, "asc", _userId);
        Assert.Equal(4, page.TotalCount);
        Assert.DoesNotContain(page.Items, t => t.Id == type.Id);

        var byId = await tags.GetTagById(type.Id, _userId);
        Assert.True(byId.IsRecordType);
        Assert.False(byId.IsLoggable);

        await Assert.ThrowsAsync<TagInRecordException>(() => _activities.Create(new ActivityRequest
        {
            PrimaryTagId = type.Id,
            DateStarted = At(8),
            Description = "1"
        }, _userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tags.Delete(type.Id, _userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tags.Update(type.Id, new TagRequest { Tag = "Renamed", TypeId = InputTypeIds.Integer }, _userId));
        await Assert.ThrowsAsync<ArgumentException>(() => _rules.Create(new TagRuleRequest
        {
            Name = "From the type",
            TargetTagId = plain.Id,
            Sources = [new() { SourceTagId = type.Id, Factor = 1 }]
        }, _userId));
        await Assert.ThrowsAsync<ArgumentException>(() => _rules.Create(new TagRuleRequest
        {
            Name = "Into the type",
            TargetTagId = type.Id,
            Sources = [new() { SourceTagId = plain.Id, Factor = 1 }]
        }, _userId));

        var export = new ExportService(_context, NullLogger<ExportService>.Instance);
        Assert.DoesNotContain(await export.GetAvailableTags(_userId), t => t.Id == type.Id);

        _context.ChangeTracker.Clear();
        Assert.Equal("Blood pressure", (await _context.Tags.SingleAsync(t => t.Id == type.Id)).TagName);
        Assert.False(await _context.Activities.AnyAsync());
    }

    [Fact]
    public async Task ARecordType_NeedsAUniqueName_AndFieldsThatDoNotClashWithTags()
    {
        await BloodPressure();
        await AddTag("Weight: Morning");

        await Assert.ThrowsAsync<ArgumentException>(() => BloodPressure());
        await Assert.ThrowsAsync<ArgumentException>(() => _types.Create(new RecordTypeRequest { Name = "Weight", Fields = [Field("Morning")] }, _userId));
        await Assert.ThrowsAsync<ArgumentException>(() => _types.Create(new RecordTypeRequest { Name = "Empty" }, _userId));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _types.Create(new RecordTypeRequest { Name = "Twice", Fields = [Field("A"), Field("a")] }, _userId));
    }

    [Fact]
    public async Task ARecord_StoresItsValuesTogether_AtTheRecordsTime()
    {
        var type = await BloodPressure();

        var record = await Log(type, At(7), "128", "82", " ");

        Assert.Equal(["Systolic", "Diastolic"], record.Values.Select(v => v.FieldName));
        Assert.Equal(["128", "82"], record.Values.Select(v => v.Value));
        var stored = await Values(type.Fields[0].TagId);
        Assert.Equal(record.Id, Assert.Single(stored).RecordId);
        Assert.Equal(At(7), stored[0].DateStarted);
        Assert.Empty(await Values(type.Fields[2].TagId)); // an empty value is not stored
    }

    [Fact]
    public async Task ARecord_NeedsAValue_AndValuesThatFitTheirFields()
    {
        var type = await BloodPressure();

        await Assert.ThrowsAsync<ArgumentException>(() => Log(type, At(7), "", null, " "));
        await Assert.ThrowsAsync<ArgumentException>(() => Log(type, At(7), "12.5"));
        await Assert.ThrowsAsync<ArgumentException>(() => Log(type, At(7), "high"));

        var other = await AddTag("Other");
        await Assert.ThrowsAsync<ArgumentException>(() => _records.Create(new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = At(7),
            Values = [new() { TagId = other.Id, Value = "1" }]
        }, _userId));
        Assert.False(await _context.Records.AnyAsync());
    }

    [Fact]
    public async Task OncePerDay_AllowsOneRecordADay()
    {
        var type = await BloodPressure(oncePerDay: true);
        Assert.False((await _context.Tags.SingleAsync(t => t.Id == type.Id)).IsRepeatable);
        await Log(type, At(7), "128");
        var yesterday = await Log(type, At(7, daysAgo: 1), "130");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Log(type, At(19), "120"));

        // Moving yesterday's record onto today is refused as well; editing it in place is not.
        var move = new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = At(20),
            Values = [new() { TagId = type.Fields[0].TagId, Value = "130" }]
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _records.Update(yesterday.Id, move, _userId));
        move.DateStarted = At(20, daysAgo: 1);
        await _records.Update(yesterday.Id, move, _userId);
    }

    [Fact]
    public async Task UpdatingARecord_MovesEveryValue_AndRemovesEmptiedOnes()
    {
        var type = await BloodPressure();
        var record = await Log(type, At(7), "128", "82", "64");

        var updated = await _records.Update(record.Id, new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = At(9, daysAgo: 1),
            Values =
            [
                new() { TagId = type.Fields[0].TagId, Value = "125" },
                new() { TagId = type.Fields[1].TagId, Value = "" },
                new() { TagId = type.Fields[2].TagId, Value = "66" }
            ]
        }, _userId);

        Assert.Equal(["125", "66"], updated.Values.Select(v => v.Value));
        Assert.All(updated.Values, v => Assert.Equal(At(9, daysAgo: 1), _context.Activities.Single(a => a.Id == v.ActivityId).DateStarted));
        Assert.Empty(await Values(type.Fields[1].TagId));

        var other = await _types.Create(new RecordTypeRequest { Name = "Other", Fields = [Field("X")] }, _userId);
        await Assert.ThrowsAsync<ArgumentException>(() => _records.Update(record.Id, new RecordRequest
        {
            RecordTypeId = other.Id,
            DateStarted = At(9),
            Values = [new() { TagId = other.Fields[0].TagId, Value = "1" }]
        }, _userId));
    }

    [Fact]
    public async Task DeletingARecord_RemovesItsValues()
    {
        var type = await BloodPressure();
        var record = await Log(type, At(7), "128", "82", "64");
        var kept = await Log(type, At(19), "120");

        await _records.Delete(record.Id, _userId);

        Assert.Equal(kept.Id, Assert.Single(await Values(type.Fields[0].TagId)).RecordId);
        Assert.Empty(await Values(type.Fields[1].TagId));
        Assert.Equal(kept.Id, Assert.Single(await _context.Records.ToListAsync()).Id);
    }

    [Fact]
    public async Task AFieldCannotBeWrittenOnItsOwn()
    {
        var type = await BloodPressure();
        var record = await Log(type, At(7), "128");
        var value = Assert.Single(await Values(type.Fields[0].TagId));
        var plain = await AddTag("Plain");

        await Assert.ThrowsAsync<TagInRecordException>(() => _activities.Create(new ActivityRequest
        {
            PrimaryTagId = type.Fields[0].TagId,
            DateStarted = At(8),
            Description = "120"
        }, _userId));
        await Assert.ThrowsAsync<TagInRecordException>(() => _activities.Update(value.Id, new ActivityRequest
        {
            PrimaryTagId = type.Fields[0].TagId,
            DateStarted = At(8),
            Description = "120"
        }, _userId));
        await Assert.ThrowsAsync<TagInRecordException>(() => _activities.Update(value.Id, new ActivityRequest
        {
            PrimaryTagId = plain.Id,
            DateStarted = At(8),
            Description = "120"
        }, _userId));
        await Assert.ThrowsAsync<TagInRecordException>(() => _activities.Delete(value.Id, _userId));

        var tags = new TagService(_context, new TagRepository(_context), NullLogger<TagService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tags.Delete(type.Fields[0].TagId, _userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tags.Update(type.Fields[0].TagId, new TagRequest { Tag = "Renamed", TypeId = InputTypeIds.Integer }, _userId));

        Assert.Equal("128", Assert.Single(await Values(type.Fields[0].TagId)).Description);
        Assert.Equal(record.Id, (await Values(type.Fields[0].TagId))[0].RecordId);
    }

    [Fact]
    public async Task ARule_ReadsAFieldsValuesLikeAnyTags()
    {
        // A field's values are not combined first: each record's value counts, as each entry of
        // any other tag does.
        var type = await _types.Create(new RecordTypeRequest { Name = "Run", Fields = [Field("Distance", InputTypeIds.Decimal)] }, _userId);
        var total = await AddTag("Distance total");
        await _rules.Create(new TagRuleRequest
        {
            Name = "Distance",
            TargetTagId = total.Id,
            Sources = [new() { SourceTagId = type.Fields[0].TagId, Factor = 1 }]
        }, _userId);

        await Log(type, At(7), "5");
        await Log(type, At(18), "3");

        Assert.Equal("8", Assert.Single(await Values(total.Id)).Description);
    }

    [Fact]
    public async Task AFieldCannotHoldARulesResults()
    {
        var type = await BloodPressure();
        var source = await AddTag("Source");

        await Assert.ThrowsAsync<ArgumentException>(() => _rules.Create(new TagRuleRequest
        {
            Name = "Into a field",
            TargetTagId = type.Fields[0].TagId,
            Sources = [new() { SourceTagId = source.Id, Factor = 1 }]
        }, _userId));
    }

    [Fact]
    public async Task UpdatingARecordType_RenamesItsFields_AndRemovesALeftOutFieldWithItsValues()
    {
        var type = await BloodPressure();
        var withPulseOnly = await Log(type, At(7), null, null, "64");
        var full = await Log(type, At(8), "128", "82", "64");

        var updated = await _types.Update(type.Id, new RecordTypeRequest
        {
            Name = "BP",
            Fields =
            [
                new() { TagId = type.Fields[1].TagId, Name = "Diastolic", TypeId = InputTypeIds.Integer },
                new() { TagId = type.Fields[0].TagId, Name = "Systolic", TypeId = InputTypeIds.Integer },
                new() { Name = "Arm", TypeId = InputTypeIds.String }
            ]
        }, _userId);

        Assert.Equal(["Diastolic", "Systolic", "Arm"], updated.Fields.Select(f => f.Name));
        Assert.Equal(["BP: Diastolic", "BP: Systolic", "BP: Arm"], updated.Fields.Select(f => f.TagName));
        Assert.False(await _context.Tags.AnyAsync(t => t.Id == type.Fields[2].TagId));
        Assert.Empty(await Values(type.Fields[2].TagId));
        // The record that had only a pulse has no value left and is gone; the other keeps two.
        Assert.False(await _context.Records.AnyAsync(r => r.Id == withPulseOnly.Id));
        Assert.Equal(2, (await _records.GetById(full.Id, _userId)).Values.Count);
        Assert.Equal(1, updated.RecordCount);
    }

    [Fact]
    public async Task AFieldARuleReads_StaysUntilTheRuleChanges()
    {
        var type = await BloodPressure();
        var total = await AddTag("Total");
        await _rules.Create(new TagRuleRequest
        {
            Name = "Pulse",
            TargetTagId = total.Id,
            Sources = [new() { SourceTagId = type.Fields[2].TagId, Factor = 1 }]
        }, _userId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _types.Update(type.Id, new RecordTypeRequest
        {
            Name = "Blood pressure",
            Fields = [new() { TagId = type.Fields[0].TagId, Name = "Systolic", TypeId = InputTypeIds.Integer }]
        }, _userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _types.Delete(type.Id, _userId));
    }

    [Fact]
    public async Task DeletingARecordType_RemovesItsRecordsValuesAndFields()
    {
        var type = await BloodPressure();
        await Log(type, At(7), "128", "82", "64");
        var plain = await AddTag("Plain");

        await _types.Delete(type.Id, _userId);

        _context.ChangeTracker.Clear();
        Assert.False(await _context.Tags.AnyAsync(t => t.IsRecordType));
        Assert.False(await _context.Records.AnyAsync());
        Assert.False(await _context.Activities.AnyAsync());
        Assert.Equal(plain.Id, Assert.Single(await _context.Tags.ToListAsync()).Id);
    }

    [Fact]
    public async Task BackupRoundTrips_KeepRecordsTogether()
    {
        var type = await BloodPressure();
        await Log(type, At(7), "128", "82", "64");
        await Log(type, At(19), "120", null, "70");
        await Log(type, At(7, daysAgo: 1), "128", "82", "64");
        var plain = await AddTag("Plain");
        await _activities.Create(new ActivityRequest { PrimaryTagId = plain.Id, DateStarted = At(9), Description = "1" }, _userId);
        var backups = new BackupService(_context, NullLogger<BackupService>.Instance, new TagRuleEngine(_context));

        // Full backup: fields and their values travel with the record type, not as tags and activities.
        var export = await backups.ExportDataAsync(_userId);
        Assert.Equal(["Plain"], export.Tags.Select(t => t.TagName));
        Assert.Equal(["Plain"], export.Activities.Select(a => a.TagName));
        var exportedType = Assert.Single(export.RecordTypes);
        Assert.Equal(["Systolic", "Diastolic", "Pulse"], exportedType.Fields.Select(f => f.Name));
        Assert.Equal(3, exportedType.Records.Count);

        await backups.ClearDataAsync(_userId);
        Assert.False(await _context.Records.AnyAsync());
        var imported = await backups.ImportDataAsync(export, clearExistingData: false, _userId);
        Assert.True(imported.Success, string.Join("; ", imported.Errors));
        Assert.Equal(1, imported.Statistics.RecordTypesImported);
        Assert.Equal(3, imported.Statistics.RecordsImported);
        await AssertRestored();

        // Secure backup: the same round trip through the per-user format.
        var secure = await backups.CreateSecureBackup(_userId);
        Assert.DoesNotContain(secure.Tags, t => t.TagName.StartsWith("Blood pressure", StringComparison.Ordinal));
        await backups.ClearUserData(_userId);
        var restored = await backups.RestoreSecureBackup(secure, _userId);
        Assert.True(restored.Success, string.Join("; ", restored.Errors));
        await AssertRestored();

        // Restoring again over the existing type skips it rather than duplicating its records.
        var again = await backups.RestoreSecureBackup(secure, _userId);
        Assert.Equal(1, again.Statistics.RecordTypesSkipped);
        Assert.Equal(3, await _context.Records.CountAsync());
    }

    [Fact]
    public async Task ARecordType_MayShareItsNameWithATag_AndBackupsKeepThemApart()
    {
        // Names are unique among record types only, so a backup must not mix the two up.
        var plain = await AddTag("Blood pressure");
        await _activities.Create(new ActivityRequest { PrimaryTagId = plain.Id, DateStarted = At(9), Description = "1" }, _userId);
        var type = await BloodPressure();
        await Log(type, At(7), "128", "82", "64");
        var backups = new BackupService(_context, NullLogger<BackupService>.Instance, new TagRuleEngine(_context));

        var export = await backups.ExportDataAsync(_userId);
        Assert.Equal(["Blood pressure"], export.Tags.Select(t => t.TagName));
        Assert.Equal("Blood pressure", Assert.Single(export.RecordTypes).Name);

        await backups.ClearDataAsync(_userId);
        var imported = await backups.ImportDataAsync(export, clearExistingData: false, _userId);
        Assert.True(imported.Success, string.Join("; ", imported.Errors));

        _context.ChangeTracker.Clear();
        var restoredPlain = await _context.Tags.SingleAsync(t => t.TagName == "Blood pressure" && !t.IsRecordType);
        Assert.Equal("1", Assert.Single(await Values(restoredPlain.Id)).Description);
        var restoredType = Assert.Single(await _types.GetAll(_userId));
        Assert.Equal(1, restoredType.RecordCount);
    }

    private async Task AssertRestored()
    {
        _context.ChangeTracker.Clear();
        var types = await _types.GetAll(_userId);
        var type = Assert.Single(types);
        Assert.Equal(3, type.RecordCount);
        var records = await _context.Records.Include(r => r.Values).OrderBy(r => r.DateStarted).ToListAsync();
        Assert.Equal([3, 3, 2], records.Select(r => r.Values.Count));
        Assert.All(records, r => Assert.All(r.Values, v => Assert.Equal(r.DateStarted, v.DateStarted)));
        Assert.Equal(["128", "82", "64"], records[1].Values.OrderBy(v => v.TagId).Select(v => v.Description));
    }
}
