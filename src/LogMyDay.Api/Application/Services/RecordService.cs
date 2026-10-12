using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LogMyDay.Api.Application.Services;

/// <summary>Records: one measurement of several fields at one moment. The record row and its values
/// (activities on the field tags) are written together in one transaction; activities are never
/// written through <see cref="ActivityService"/> for a field. Day locks do not apply to fields: a
/// record type's "once per day" is the per-day limit.</summary>
public class RecordService : IRecordService
{
    private readonly LogMyDayDbContext _context;
    private readonly IEventLogService _eventLogService;
    private readonly ITagRuleEngine _tagRuleEngine;

    public RecordService(LogMyDayDbContext context, IEventLogService eventLogService, ITagRuleEngine tagRuleEngine)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _eventLogService = eventLogService ?? throw new ArgumentNullException(nameof(eventLogService));
        _tagRuleEngine = tagRuleEngine ?? throw new ArgumentNullException(nameof(tagRuleEngine));
    }

    public async Task<RecordResponse> GetById(int id, Guid userId)
    {
        var record = await _context.Records
            .AsNoTracking()
            .Include(r => r.Values)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId)
            ?? throw new KeyNotFoundException("Record not found");
        var type = await LoadType(record.RecordTypeId, userId);

        return MapToResponse(record, type);
    }

    public async Task<RecordResponse> Create(RecordRequest request, Guid userId)
    {
        var type = await LoadType(request.RecordTypeId, userId);
        var dateStarted = await ToUserLocalTime(userId, request.DateStarted);
        var values = await NormalizeValues(type, request.Values);
        await EnsureOncePerDay(type, dateStarted, exceptRecordId: null);

        await using var transaction = await BeginWriteTransaction();

        var record = new Record
        {
            UserId = userId,
            RecordTypeId = type.Id,
            DateStarted = dateStarted,
            DateCreated = DateTime.UtcNow
        };
        _context.Records.Add(record);
        await _context.SaveChangesAsync();

        foreach (var (field, value) in values)
        {
            _context.Activities.Add(NewValue(record, field, value));
        }

        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Record '{type.Name}' on {record.DateStarted:yyyy-MM-dd HH:mm} added: {Describe(type, values)}");

        var day = DateOnly.FromDateTime(record.DateStarted);
        await _tagRuleEngine.OnSourcesChanged(userId, values.Select(v => (v.Field.Id, day)).ToList());
        await CommitIfOwned(transaction);

        return await GetById(record.Id, userId);
    }

    public async Task<RecordResponse> Update(int id, RecordRequest request, Guid userId)
    {
        var record = await _context.Records
            .Include(r => r.Values)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId)
            ?? throw new KeyNotFoundException("Record not found");
        if (request.RecordTypeId != 0 && request.RecordTypeId != record.RecordTypeId)
        {
            throw new ArgumentException("The record type of a record cannot change.");
        }

        var type = await LoadType(record.RecordTypeId, userId);
        var dateStarted = await ToUserLocalTime(userId, request.DateStarted);
        var values = await NormalizeValues(type, request.Values);
        await EnsureOncePerDay(type, dateStarted, exceptRecordId: record.Id);

        // The days the record's values leave, for rules that read its fields.
        var previousDay = DateOnly.FromDateTime(record.DateStarted);
        var changes = record.Values.Select(v => (v.TagId, previousDay)).ToList();

        await using var transaction = await BeginWriteTransaction();

        record.DateStarted = dateStarted;
        var byField = values.ToDictionary(v => v.Field.Id, v => v.Value);
        foreach (var existing in record.Values.ToList())
        {
            if (byField.Remove(existing.TagId, out var value))
            {
                existing.Description = value;
                existing.DateStarted = dateStarted;
            }
            else
            {
                record.Values.Remove(existing);
                _context.Activities.Remove(existing);
            }
        }

        foreach (var (fieldId, value) in byField)
        {
            _context.Activities.Add(NewValue(record, type.Fields.First(f => f.Id == fieldId), value));
        }

        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Record '{type.Name}' on {record.DateStarted:yyyy-MM-dd HH:mm} updated: {Describe(type, values)}");

        var day = DateOnly.FromDateTime(dateStarted);
        changes.AddRange(values.Select(v => (v.Field.Id, day)));
        await _tagRuleEngine.OnSourcesChanged(userId, changes);
        await CommitIfOwned(transaction);

        return await GetById(record.Id, userId);
    }

    public async Task Delete(int id, Guid userId)
    {
        var record = await _context.Records
            .Include(r => r.RecordType)
            .Include(r => r.Values)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId)
            ?? throw new KeyNotFoundException("Record not found");

        await using var transaction = await BeginWriteTransaction();

        var day = DateOnly.FromDateTime(record.DateStarted);
        var changes = record.Values.Select(v => (v.TagId, day)).ToList();
        _context.Activities.RemoveRange(record.Values);
        _context.Records.Remove(record);
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Record '{record.RecordType?.Name ?? "?"}' on {record.DateStarted:yyyy-MM-dd HH:mm} deleted with {changes.Count} value(s)");

        await _tagRuleEngine.OnSourcesChanged(userId, changes);
        await CommitIfOwned(transaction);
    }

    // Each value must name a field of the type, at most once, and fit the field. Empty values are
    // left out; at least one must remain. The result is in the type's field order.
    private async Task<List<(Tag Field, string Value)>> NormalizeValues(RecordType type, List<RecordValueRequest>? values)
    {
        var requested = values ?? [];
        if (requested.GroupBy(v => v.TagId).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("A field appears twice.");
        }

        var fields = type.Fields.ToDictionary(f => f.Id);
        var listIds = type.Fields.Where(f => f.OptionListId != null).Select(f => f.OptionListId!.Value).Distinct().ToList();
        var options = (await _context.TagOptions.Where(o => listIds.Contains(o.OptionListId)).ToListAsync())
            .ToLookup(o => o.OptionListId);

        var result = new List<(Tag Field, string Value)>();
        foreach (var value in requested)
        {
            if (!fields.TryGetValue(value.TagId, out var field))
            {
                throw new ArgumentException($"Tag {value.TagId} is not a field of '{type.Name}'.");
            }

            var fieldName = RecordFieldValues.FieldName(type.Name, field.TagName);
            var fieldOptions = field.OptionListId is int listId ? options[listId].ToList() : null;
            var stored = RecordFieldValues.Normalize(field, fieldName, value.Value, fieldOptions);
            if (stored != null)
            {
                result.Add((field, stored));
            }
        }

        if (result.Count == 0)
        {
            throw new ArgumentException("A record needs at least one value.");
        }

        return result.OrderBy(v => v.Field.RecordFieldOrder).ThenBy(v => v.Field.Id).ToList();
    }

    private async Task EnsureOncePerDay(RecordType type, DateTime dateStarted, int? exceptRecordId)
    {
        if (!type.OncePerDay)
        {
            return;
        }

        var dayStart = dateStarted.Date;
        var dayEnd = dayStart.AddDays(1);
        if (await _context.Records.AnyAsync(r => r.RecordTypeId == type.Id && r.Id != exceptRecordId
                && r.DateStarted >= dayStart && r.DateStarted < dayEnd))
        {
            throw new InvalidOperationException($"'{type.Name}' is logged once per day and already has a record on {dayStart:yyyy-MM-dd}.");
        }
    }

    private static Activity NewValue(Record record, Tag field, string value)
    {
        return new Activity
        {
            UserId = record.UserId,
            TagId = field.Id,
            RecordId = record.Id,
            DateStarted = record.DateStarted,
            DateCreated = DateTime.UtcNow,
            Description = value
        };
    }

    private static string Describe(RecordType type, List<(Tag Field, string Value)> values)
    {
        return string.Join(", ", values.Select(v => $"{RecordFieldValues.FieldName(type.Name, v.Field.TagName)} {v.Value}"));
    }

    private async Task<RecordType> LoadType(int id, Guid userId)
    {
        return await _context.RecordTypes
            .Include(t => t.Fields).ThenInclude(f => f.Unit)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId)
            ?? throw new KeyNotFoundException("Record type not found");
    }

    /// <summary>Record times are naive local time in the user's time zone, as for activities: an
    /// unspecified value is kept, one that carries a kind is converted.</summary>
    private async Task<DateTime> ToUserLocalTime(Guid userId, DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            return value;
        }

        var timeZone = await _context.Users.Where(u => u.Id == userId).Select(u => u.TimeZone).FirstOrDefaultAsync();

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
        }

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(value.ToUniversalTime(), tz), DateTimeKind.Unspecified);
    }

    private async Task<IDbContextTransaction?> BeginWriteTransaction()
    {
        return _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync()
            : null;
    }

    private static async Task CommitIfOwned(IDbContextTransaction? transaction)
    {
        if (transaction != null)
        {
            await transaction.CommitAsync();
        }
    }

    private static RecordResponse MapToResponse(Record record, RecordType type)
    {
        var fields = type.Fields.ToDictionary(f => f.Id);

        return new RecordResponse
        {
            Id = record.Id,
            RecordTypeId = record.RecordTypeId,
            RecordTypeName = type.Name,
            DateStarted = record.DateStarted,
            DateCreated = record.DateCreated,
            Values = record.Values
                .Where(v => fields.ContainsKey(v.TagId))
                .Select(v => (Value: v, Field: fields[v.TagId]))
                .OrderBy(v => v.Field.RecordFieldOrder)
                .ThenBy(v => v.Field.Id)
                .Select(v => new RecordValueResponse
                {
                    ActivityId = v.Value.Id,
                    TagId = v.Field.Id,
                    FieldName = RecordFieldValues.FieldName(type.Name, v.Field.TagName),
                    InputTypeId = v.Field.InputTypeId,
                    UnitSymbol = v.Field.Unit?.Symbol,
                    Value = v.Value.Description ?? string.Empty
                })
                .ToList()
        };
    }
}
