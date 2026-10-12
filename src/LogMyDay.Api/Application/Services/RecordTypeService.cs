using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Domain.Helpers;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LogMyDay.Api.Application.Services;

public class RecordTypeService : IRecordTypeService
{
    private const int MaxFields = 20;
    private const int MaxNameLength = 100;

    private readonly LogMyDayDbContext _context;
    private readonly ITagRuleEngine _tagRuleEngine;
    private readonly IEventLogService _eventLogService;

    public RecordTypeService(LogMyDayDbContext context, ITagRuleEngine tagRuleEngine, IEventLogService eventLogService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tagRuleEngine = tagRuleEngine ?? throw new ArgumentNullException(nameof(tagRuleEngine));
        _eventLogService = eventLogService ?? throw new ArgumentNullException(nameof(eventLogService));
    }

    public async Task<IList<RecordTypeResponse>> GetAll(Guid userId)
    {
        var types = await TypesWithFields()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .ToListAsync();

        var recordCounts = await _context.Records
            .Where(r => r.UserId == userId)
            .GroupBy(r => r.RecordTypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(c => c.TypeId, c => c.Count);
        var valueCounts = await ValueCounts(userId);

        return types.Select(t => MapToResponse(t, recordCounts.GetValueOrDefault(t.Id), valueCounts)).ToList();
    }

    public async Task<RecordTypeResponse> GetById(int id, Guid userId)
    {
        var type = await LoadType(id, userId);
        var recordCount = await _context.Records.CountAsync(r => r.RecordTypeId == id);

        return MapToResponse(type, recordCount, await ValueCounts(userId));
    }

    public async Task<RecordTypeResponse> Create(RecordTypeRequest request, Guid userId)
    {
        var name = await ValidateType(request, userId, typeId: null);
        var (fields, inputTypes) = await ValidateFields(request, name, userId, existing: null);

        await using var transaction = await BeginWriteTransaction();

        var displayOrder = await _context.RecordTypes.Where(t => t.UserId == userId).MaxAsync(t => (int?)t.DisplayOrder) ?? 0;
        var type = new RecordType
        {
            UserId = userId,
            Name = name,
            GroupId = request.GroupId,
            OncePerDay = request.OncePerDay,
            DisplayOrder = displayOrder + 1,
            DateCreated = DateTime.UtcNow
        };
        _context.RecordTypes.Add(type);
        await _context.SaveChangesAsync();

        for (var i = 0; i < fields.Count; i++)
        {
            var tag = new Tag { TagName = string.Empty, UserId = userId };
            ApplyField(tag, type, fields[i], i, inputTypes[fields[i].TypeId]);
            _context.Tags.Add(tag);
        }

        await _context.SaveChangesAsync();
        await CommitIfOwned(transaction);

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Record type '{type.Name}' created with fields {string.Join(", ", fields.Select(f => $"'{f.Name}'"))}{(type.OncePerDay ? " (once per day)" : string.Empty)}");

        return await GetById(type.Id, userId);
    }

    public async Task<RecordTypeResponse> Update(int id, RecordTypeRequest request, Guid userId)
    {
        var type = await LoadType(id, userId);
        var name = await ValidateType(request, userId, typeId: id);
        var (fields, inputTypes) = await ValidateFields(request, name, userId, existing: type);

        var keptIds = fields.Where(f => f.TagId != null).Select(f => f.TagId!.Value).ToHashSet();
        var removed = type.Fields.Where(f => !keptIds.Contains(f.Id)).ToList();
        var removedIds = removed.Select(f => f.Id).ToList();
        if (removedIds.Count > 0)
        {
            await EnsureNotRuleSource(removedIds);
        }

        // A changed "combine per day" changes the day value every rule reading the field computes.
        var combineChanged = fields
            .Where(f => f.TagId != null)
            .Where(f => type.Fields.First(t => t.Id == f.TagId).DayCombine != RecordFieldValues.DayCombineFor(f.TypeId, f.OptionListId, f.DayCombine))
            .Select(f => f.TagId!.Value)
            .ToList();

        await using var transaction = await BeginWriteTransaction();

        var removedValues = 0;
        if (removedIds.Count > 0)
        {
            var values = await _context.Activities.Where(a => a.UserId == userId && removedIds.Contains(a.TagId)).ToListAsync();
            removedValues = values.Count;
            _context.Activities.RemoveRange(values);
            _context.Tags.RemoveRange(removed);
            await _context.SaveChangesAsync();

            // A record needs at least one value, so a record whose only values were removed goes too.
            var emptyRecords = await _context.Records
                .Where(r => r.RecordTypeId == type.Id && !_context.Activities.Any(a => a.RecordId == r.Id))
                .ToListAsync();
            _context.Records.RemoveRange(emptyRecords);
        }

        type.Name = name;
        type.GroupId = request.GroupId;
        type.OncePerDay = request.OncePerDay;

        for (var i = 0; i < fields.Count; i++)
        {
            var tag = fields[i].TagId is int tagId
                ? type.Fields.First(f => f.Id == tagId)
                : null;
            if (tag == null)
            {
                tag = new Tag { TagName = string.Empty, UserId = userId };
                _context.Tags.Add(tag);
            }

            ApplyField(tag, type, fields[i], i, inputTypes[fields[i].TypeId]);
        }

        await _context.SaveChangesAsync();
        await RecomputeRulesReading(combineChanged, userId);
        await CommitIfOwned(transaction);

        var removedText = removed.Count > 0
            ? $"; removed {string.Join(", ", removed.Select(f => $"'{RecordFieldValues.FieldName(name, f.TagName)}'"))} with {removedValues} value(s)"
            : string.Empty;
        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Record type '{type.Name}' updated: fields {string.Join(", ", fields.Select(f => $"'{f.Name}'"))}{removedText}");

        return await GetById(type.Id, userId);
    }

    public async Task Delete(int id, Guid userId)
    {
        var type = await LoadType(id, userId);
        var fieldIds = type.Fields.Select(f => f.Id).ToList();
        if (fieldIds.Count > 0)
        {
            await EnsureNotRuleSource(fieldIds);
        }

        await using var transaction = await BeginWriteTransaction();

        var values = await _context.Activities.Where(a => a.UserId == userId && fieldIds.Contains(a.TagId)).ToListAsync();
        var records = await _context.Records.Where(r => r.RecordTypeId == type.Id).ToListAsync();
        _context.Activities.RemoveRange(values);
        _context.Records.RemoveRange(records);
        _context.Tags.RemoveRange(type.Fields);
        _context.RecordTypes.Remove(type);
        await _context.SaveChangesAsync();
        await CommitIfOwned(transaction);

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Record type '{type.Name}' deleted with {records.Count} record(s) and {values.Count} value(s)");
    }

    // The type's name is unique among the user's record types; its group is the user's own.
    private async Task<string> ValidateType(RecordTypeRequest request, Guid userId, int? typeId)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            throw new ArgumentException($"A record type needs a name of at most {MaxNameLength} characters.");
        }

        var names = await _context.RecordTypes
            .Where(t => t.UserId == userId && t.Id != typeId)
            .Select(t => t.Name)
            .ToListAsync();
        if (names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"A record type named '{name}' already exists.");
        }

        if (request.GroupId is int groupId && !await _context.TagGroups.AnyAsync(g => g.Id == groupId && g.UserId == userId))
        {
            throw new KeyNotFoundException("Tag group not found");
        }

        return name;
    }

    // Fields have unique names within the type, an input type, and an option list and unit that
    // exist. A field's tag name ("{type}: {field}") must not clash with another of the user's tags.
    private async Task<(List<RecordFieldRequest> Fields, Dictionary<int, InputType> InputTypes)> ValidateFields(
        RecordTypeRequest request, string typeName, Guid userId, RecordType? existing)
    {
        var fields = request.Fields ?? [];
        if (fields.Count == 0)
        {
            throw new ArgumentException("A record type needs at least one field.");
        }

        if (fields.Count > MaxFields)
        {
            throw new ArgumentException($"A record type can have at most {MaxFields} fields.");
        }

        foreach (var field in fields)
        {
            field.Name = field.Name?.Trim() ?? string.Empty;
            if (field.Name.Length == 0 || field.Name.Length > MaxNameLength)
            {
                throw new ArgumentException($"Every field needs a name of at most {MaxNameLength} characters.");
            }

            if (field.TagId is int tagId && existing?.Fields.Any(f => f.Id == tagId) != true)
            {
                throw new KeyNotFoundException($"Field '{field.Name}' does not belong to this record type.");
            }

            if (field.MinValue.HasValue && field.MaxValue.HasValue && field.MinValue > field.MaxValue)
            {
                throw new ArgumentException($"The minimum of {field.Name} is above its maximum.");
            }
        }

        var duplicate = fields.GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"Two fields are named '{duplicate.Key}'.");
        }

        if (fields.Where(f => f.TagId != null).GroupBy(f => f.TagId).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("A field appears twice.");
        }

        var typeIds = fields.Select(f => f.TypeId).Distinct().ToList();
        var inputTypes = await _context.InputTypes.Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        var unknownType = fields.FirstOrDefault(f => !inputTypes.ContainsKey(f.TypeId));
        if (unknownType != null)
        {
            throw new ArgumentException($"Choose a type for {unknownType.Name}.");
        }

        var listIds = fields.Where(f => f.OptionListId != null).Select(f => f.OptionListId!.Value).Distinct().ToList();
        var knownLists = await _context.TagOptionLists
            .Where(l => listIds.Contains(l.Id) && (l.UserId == userId || l.UserId == null))
            .Select(l => l.Id)
            .ToListAsync();
        if (listIds.Any(l => !knownLists.Contains(l)))
        {
            throw new KeyNotFoundException("Option list not found");
        }

        var unitIds = fields.Where(f => f.UnitId != null).Select(f => f.UnitId!.Value).Distinct().ToList();
        var knownUnits = await _context.Units.Where(u => unitIds.Contains(u.Id)).Select(u => u.Id).ToListAsync();
        if (unitIds.Any(u => !knownUnits.Contains(u)))
        {
            throw new KeyNotFoundException("Unit not found");
        }

        var ownFieldIds = existing?.Fields.Select(f => f.Id).ToList() ?? [];
        var otherNames = (await _context.Tags
                .Where(t => t.UserId == userId && !ownFieldIds.Contains(t.Id))
                .Select(t => t.TagName)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clash = fields.Select(f => RecordFieldValues.TagName(typeName, f.Name)).FirstOrDefault(otherNames.Contains);
        if (clash != null)
        {
            throw new ArgumentException($"A tag named '{clash}' already exists. Rename the field or the record type.");
        }

        return (fields, inputTypes);
    }

    // A field's tag follows its record type: its name, group and position. An input type whose
    // range or step cannot be edited keeps its fixed values, as for any tag.
    private static void ApplyField(Tag tag, RecordType type, RecordFieldRequest field, int order, InputType inputType)
    {
        var constraints = InputTypeDefaults.GetConstraintsForType(field.TypeId);
        var minValue = inputType.IsMinimumEditable ? field.MinValue : constraints.MinValue;
        var maxValue = inputType.IsMaximumEditable ? field.MaxValue : constraints.MaxValue;
        var step = inputType.IsStepEditable ? field.Step : constraints.Step;

        tag.TagName = RecordFieldValues.TagName(type.Name, field.Name);
        tag.Description = field.Description;
        tag.InputTypeId = field.TypeId;
        tag.IsRequired = false;
        tag.IsRepeatable = true;
        tag.TimeGranularity = TimeGranularity.Exact;
        tag.IsRange = false;
        tag.UnitId = field.UnitId;
        tag.MinValue = minValue;
        tag.MaxValue = maxValue;
        tag.Step = step;
        tag.DefaultValue = field.DefaultValue;
        tag.OptionListId = field.OptionListId;
        tag.GroupId = type.GroupId;
        tag.RecordType = type;
        tag.RecordFieldOrder = order;
        tag.DayCombine = RecordFieldValues.DayCombineFor(field.TypeId, field.OptionListId, field.DayCombine);
    }

    // A field a rule reads stays until the rule is changed or deleted (the database would refuse
    // it anyway: Restrict), as for any tag.
    private async Task EnsureNotRuleSource(List<int> tagIds)
    {
        var use = await _context.TagRuleSources
            .Where(s => tagIds.Contains(s.SourceTagId))
            .Select(s => new { RuleName = s.Rule!.Name, s.SourceTag!.TagName })
            .FirstOrDefaultAsync();
        if (use != null)
        {
            throw new InvalidOperationException(
                $"Field '{use.TagName}' is in use by rule '{use.RuleName}'. Change or delete the rule first.");
        }
    }

    // Rules reading a field whose day value changed are recomputed over their active range.
    private async Task RecomputeRulesReading(List<int> tagIds, Guid userId)
    {
        if (tagIds.Count == 0)
        {
            return;
        }

        var rules = await _context.TagRules
            .Include(r => r.Sources).ThenInclude(s => s.SourceTag)
            .Include(r => r.Cases)
            .Include(r => r.TargetTag)
            .Where(r => r.UserId == userId && r.IsEnabled && r.Sources.Any(s => tagIds.Contains(s.SourceTagId)))
            .ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        foreach (var rule in rules)
        {
            await _tagRuleEngine.Recompute(rule, rule.EffectiveFrom <= today ? rule.EffectiveFrom : today, today);
        }
    }

    private async Task<Dictionary<int, int>> ValueCounts(Guid userId)
    {
        return await _context.Activities
            .Where(a => a.UserId == userId && a.RecordId != null)
            .GroupBy(a => a.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(c => c.TagId, c => c.Count);
    }

    private IQueryable<RecordType> TypesWithFields()
    {
        return _context.RecordTypes
            .Include(t => t.Group)
            .Include(t => t.Fields).ThenInclude(f => f.Unit);
    }

    private async Task<RecordType> LoadType(int id, Guid userId)
    {
        return await TypesWithFields().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId)
            ?? throw new KeyNotFoundException("Record type not found");
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

    private static RecordTypeResponse MapToResponse(RecordType type, int recordCount, IReadOnlyDictionary<int, int> valueCounts)
    {
        return new RecordTypeResponse
        {
            Id = type.Id,
            Name = type.Name,
            GroupId = type.GroupId,
            GroupName = type.Group?.Name,
            OncePerDay = type.OncePerDay,
            DisplayOrder = type.DisplayOrder,
            DateCreated = type.DateCreated,
            RecordCount = recordCount,
            Fields = type.Fields
                .OrderBy(f => f.RecordFieldOrder)
                .ThenBy(f => f.Id)
                .Select(f => new RecordFieldResponse
                {
                    TagId = f.Id,
                    Name = RecordFieldValues.FieldName(type.Name, f.TagName),
                    TagName = f.TagName,
                    Description = f.Description,
                    InputTypeId = f.InputTypeId,
                    UnitId = f.UnitId,
                    UnitSymbol = f.Unit?.Symbol,
                    MinValue = f.MinValue,
                    MaxValue = f.MaxValue,
                    Step = f.Step,
                    DefaultValue = f.DefaultValue,
                    OptionListId = f.OptionListId,
                    DayCombine = f.DayCombine ?? DayCombine.Latest,
                    Order = f.RecordFieldOrder ?? 0,
                    ValueCount = valueCounts.GetValueOrDefault(f.Id)
                })
                .ToList()
        };
    }
}
