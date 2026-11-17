using System.ComponentModel;
using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Records: one measurement of several values at one moment, e.g. blood pressure with systolic,
/// diastolic and pulse. A record type is a tag that holds no values; each field is a tag of its
/// own ("{record type}: {field}"), and a record's values are activities on the field tags. Fields
/// are written only as a whole record, never with log_value.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class RecordTools(
    McpUserContext user,
    IRecordTypeService recordTypes,
    IRecordService records,
    ITagService tags,
    ITagOptionListService optionLists,
    ITagCheckService checks,
    ITagTriggerEngine triggers,
    UserClock clock)
{
    public const string UpdateSentinelPrefix = "UPDATE_RECORD_TYPE_";
    public const string DeleteSentinelPrefix = "DELETE_RECORD_TYPE_";

    public sealed record FieldInput(
        [property: Description("Field name within the record type, e.g. \"Systolic\"; its tag is named \"{record type}: {field}\".")] string Name,
        [property: Description("Input type id, as for create_tag; required for a new field.")] int? InputTypeId,
        [property: Description("Unit id (units are global).")] int? UnitId = null,
        [property: Description("Option list id; the value must then be one of its options.")] int? OptionListId = null,
        [property: Description("Minimum for numeric types.")] double? MinValue = null,
        [property: Description("Maximum for numeric types.")] double? MaxValue = null,
        [property: Description("Step for numeric types.")] double? Step = null,
        [property: Description("Optional description.")] string? Description = null,
        [property: Description("The field's tag id, to keep an existing field (update_record_type only); omit for a new field. A kept field keeps any setting left out here; 0 removes its unit or option list, other settings cannot be cleared here.")] int? TagId = null);

    public sealed record ValueInput(
        [property: Description("The field: its name within the record type (e.g. \"Systolic\") or its tag id.")] string Field,
        [property: Description("The value: number, string or boolean, as the field's input type expects. Null leaves the field empty.")] JsonElement Value);

    [McpServerTool(Name = "list_record_types", Title = "List record types", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's record types with their fields (tag id, name, input type, unit, range, option list) and how many records each has.")]
    public Task<IList<RecordTypeResponse>> ListRecordTypes() => recordTypes.GetAll(user.UserId);

    [McpServerTool(Name = "create_record_type", Title = "Create record type", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Creates a record type and a new tag for each field, in the given order. The name must be unique among the user's record types, and no field's tag name may already exist. Referenced group and option lists must belong to the user; units are shared.")]
    public Task<RecordTypeResponse> CreateRecordType(
        [Description("Record type name, e.g. \"Blood pressure\".")] string name,
        [Description("The fields, in form order (at least one).")] List<FieldInput> fields,
        [Description("Group id; the record type and its fields are listed in it.")] int? groupId = null,
        [Description("At most one record of this type per day (default false).")] bool oncePerDay = false)
    {
        var request = new RecordTypeRequest
        {
            Name = name,
            GroupId = groupId,
            OncePerDay = oncePerDay,
            Fields = (fields ?? []).Select(f => new RecordFieldRequest
            {
                Name = f.Name,
                TypeId = f.InputTypeId ?? 0,
                UnitId = f.UnitId,
                OptionListId = f.OptionListId,
                MinValue = f.MinValue,
                MaxValue = f.MaxValue,
                Step = f.Step,
                Description = f.Description
            }).ToList()
        };

        return recordTypes.Create(request, user.UserId);
    }

    [McpServerTool(Name = "log_record", Title = "Log record", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Logs one record: the values of a record type's fields measured together at one moment. Each value is encoded for its field's input type as in log_value; fields left out or null stay empty, and at least one value is needed. The result carries warnings when the record leaves one of the user's Warn checks broken on its day (it is saved anyway) and reminders when it set one of the user's trigger reminders. Fails with conflict for a record type logged once per day that already has a record that day.")]
    public async Task<RecordResponse> LogRecord(
        [Description("Record type id or name.")] string recordType,
        [Description("The values, one per field.")] List<ValueInput> values,
        [Description("When it was measured, yyyy-MM-ddTHH:mm local; default now in the user's time zone.")] string? dateTime = null)
    {
        var type = await ResolveType(recordType);
        var request = new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = await ParseOrNow(dateTime),
            Values = []
        };

        foreach (var value in values ?? [])
        {
            if (value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            var field = ResolveField(type, value.Field);
            request.Values.Add(new RecordValueRequest { TagId = field.TagId, Value = await Encode(field, value.Value) });
        }

        var record = await records.Create(request, user.UserId);
        record.Warnings = await checks.WarningsAfterSave(user.UserId, record);
        record.Reminders = triggers.TakeCreated();

        return record;
    }

    [McpServerTool(Name = "get_record", Title = "Get record", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one record with its time and its stored values in field order.")]
    public Task<RecordResponse> GetRecord([Description("Record id.")] int recordId) => records.GetById(recordId, user.UserId);

    [McpServerTool(Name = "list_records", Title = "List records", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists records, newest first, paged; optionally of one record type and within an inclusive date range (from/to as yyyy-MM-dd). Each record carries its time and its values in field order.")]
    public async Task<PagedResult<RecordResponse>> ListRecords(
        [Description("Record type id or name.")] string? recordType = null,
        [Description("First day, inclusive (yyyy-MM-dd).")] string? from = null,
        [Description("Last day, inclusive (yyyy-MM-dd).")] string? to = null,
        [Description("1-based page number.")] int? page = null,
        [Description("Rows per page, max 200.")] int? pageSize = null)
    {
        int? typeId = recordType == null ? null : (await ResolveType(recordType)).Id;
        var start = from == null ? (DateTime?)null : DateArguments.ParseDate(from, "from").ToDateTime(TimeOnly.MinValue);
        var end = to == null ? (DateTime?)null : DateArguments.EndOfDay(DateArguments.ParseDate(to, "to"));

        return await records.GetPaged(user.UserId, typeId, start, end, PageLimits.Page(page), PageLimits.Clamp(pageSize));
    }

    [McpServerTool(Name = "update_record", Title = "Update record", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Changes a record: its time and/or some of its values. Only the fields given change; a null value empties that field, and at least one value must remain. Changing the time moves every value. The result carries warnings when the change leaves one of the user's Warn checks broken on its day (it is saved anyway) and reminders when it set one of the user's trigger reminders.")]
    public async Task<RecordResponse> UpdateRecord(
        [Description("Record id.")] int recordId,
        [Description("The values to change, one per field.")] List<ValueInput>? values = null,
        [Description("New time, yyyy-MM-ddTHH:mm local.")] string? dateTime = null)
    {
        var current = await records.GetById(recordId, user.UserId);
        var type = await ResolveType(current.RecordTypeId.ToString());
        var byField = current.Values.ToDictionary(v => v.TagId, v => (string?)v.Value);
        foreach (var value in values ?? [])
        {
            var field = ResolveField(type, value.Field);
            byField[field.TagId] = value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                ? null
                : await Encode(field, value.Value);
        }

        var request = new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = dateTime == null ? current.DateStarted : DateArguments.ParseDateTime(dateTime, "dateTime", await clock.Zone(user.UserId)),
            Values = byField.Select(v => new RecordValueRequest { TagId = v.Key, Value = v.Value }).ToList()
        };

        var record = await records.Update(recordId, request, user.UserId);
        record.Warnings = await checks.WarningsAfterSave(user.UserId, record);
        record.Reminders = triggers.TakeCreated();

        return record;
    }

    [McpServerTool(Name = "delete_record", Title = "Delete record", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes one record with all its values.")]
    public async Task<object> DeleteRecord([Description("Record id.")] int recordId)
    {
        var record = await records.GetById(recordId, user.UserId);
        await records.Delete(recordId, user.UserId);

        return new { deleted = true, recordId, recordType = record.RecordTypeName, valuesDeleted = record.Values.Count };
    }

    [McpServerTool(Name = "update_record_type", Title = "Update record type", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Changes a record type: its name, group, once per day and/or its fields; omitted arguments keep their current value. When fields is given it REPLACES the whole set, in order: a field with a tagId is kept, one without is added, and an existing field left out is removed WITH ALL ITS VALUES (records left without any value go too). Removing fields that hold values requires confirm = \"UPDATE_RECORD_TYPE_<id>\"; without it nothing changes and the response says how many values would go. Refused while a rule reads a removed field.")]
    public async Task<RecordTypeResponse> UpdateRecordType(
        [Description("Record type id or name.")] string recordType,
        [Description("New name.")] string? name = null,
        [Description("Group id; 0 removes the group.")] int? groupId = null,
        [Description("At most one record of this type per day.")] bool? oncePerDay = null,
        [Description("The complete new set of fields, in form order; see the tool description.")] List<FieldInput>? fields = null,
        [Description("Must be exactly UPDATE_RECORD_TYPE_<id> when fields with values are removed.")] string? confirm = null)
    {
        var type = await ResolveType(recordType);
        var current = type.Fields.ToDictionary(f => f.TagId);
        var request = new RecordTypeRequest
        {
            Name = name ?? type.Name,
            GroupId = groupId == null ? type.GroupId : groupId == 0 ? null : groupId,
            OncePerDay = oncePerDay ?? type.OncePerDay,
            Fields = fields == null
                ? type.Fields.OrderBy(f => f.Order).Select(f => ToRequest(f, null)).ToList()
                : fields.Select(f => f.TagId is int tagId && current.TryGetValue(tagId, out var existing)
                    ? ToRequest(existing, f)
                    : new RecordFieldRequest
                    {
                        TagId = f.TagId,
                        Name = f.Name,
                        TypeId = f.InputTypeId ?? 0,
                        UnitId = f.UnitId,
                        OptionListId = f.OptionListId,
                        MinValue = f.MinValue,
                        MaxValue = f.MaxValue,
                        Step = f.Step,
                        Description = f.Description
                    }).ToList()
        };

        var keptIds = request.Fields.Where(f => f.TagId != null).Select(f => f.TagId!.Value).ToHashSet();
        var removed = type.Fields.Where(f => !keptIds.Contains(f.TagId)).ToList();
        var removedValues = removed.Sum(f => f.ValueCount);
        if (removedValues > 0)
        {
            ConfirmSentinel.Require(confirm, UpdateSentinelPrefix + type.Id,
                $"Removes {string.Join(", ", removed.Select(f => $"'{f.Name}'"))} from record type {type.Id} '{type.Name}' with {removedValues} value(s).");
        }

        return await recordTypes.Update(type.Id, request, user.UserId);
    }

    [McpServerTool(Name = "delete_record_type", Title = "Delete record type", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes a record type with its field tags, all its records and their values. Requires confirm = \"DELETE_RECORD_TYPE_<id>\"; without it nothing is deleted and the response says how many records and values would go — tell the user before confirming. Refused while a rule reads one of its fields.")]
    public async Task<object> DeleteRecordType(
        [Description("Record type id or name.")] string recordType,
        [Description("Must be exactly DELETE_RECORD_TYPE_<id>.")] string? confirm = null)
    {
        var type = await ResolveType(recordType);
        var values = type.Fields.Sum(f => f.ValueCount);
        ConfirmSentinel.Require(confirm, DeleteSentinelPrefix + type.Id,
            $"Deletes record type {type.Id} '{type.Name}' with {type.Fields.Count} field(s), {type.RecordCount} record(s) and {values} value(s).");

        await recordTypes.Delete(type.Id, user.UserId);

        return new { deleted = true, recordTypeId = type.Id, name = type.Name, recordsDeleted = type.RecordCount, valuesDeleted = values };
    }

    // --- helpers ---

    private async Task<RecordTypeResponse> ResolveType(string recordType)
    {
        var all = await recordTypes.GetAll(user.UserId);
        var query = recordType?.Trim() ?? string.Empty;
        var match = int.TryParse(query, out var id)
            ? all.FirstOrDefault(t => t.Id == id)
            : all.FirstOrDefault(t => string.Equals(t.Name, query, StringComparison.OrdinalIgnoreCase));

        return match ?? throw new KeyNotFoundException(
            $"No record type matches '{recordType}'. Known: {string.Join(", ", all.Select(t => $"{t.Id} '{t.Name}'"))}.");
    }

    // A kept field takes the settings given and keeps the rest; 0 removes a unit or option list.
    private static RecordFieldRequest ToRequest(RecordFieldResponse field, FieldInput? change)
    {
        return new RecordFieldRequest
        {
            TagId = field.TagId,
            Name = change?.Name ?? field.Name,
            Description = change?.Description ?? field.Description,
            TypeId = change?.InputTypeId ?? field.InputTypeId ?? 0,
            UnitId = change?.UnitId is int unitId ? (unitId == 0 ? null : unitId) : field.UnitId,
            OptionListId = change?.OptionListId is int listId ? (listId == 0 ? null : listId) : field.OptionListId,
            MinValue = change?.MinValue ?? field.MinValue,
            MaxValue = change?.MaxValue ?? field.MaxValue,
            Step = change?.Step ?? field.Step,
            DefaultValue = field.DefaultValue
        };
    }

    // The value encoded for its field as log_value would; an error names the field.
    private async Task<string> Encode(RecordFieldResponse field, JsonElement value)
    {
        var tag = await tags.GetTagById(field.TagId, user.UserId);
        var options = tag.OptionListId is int listId ? await optionLists.GetById(listId, user.UserId) : null;
        try
        {
            return ValueEncoder.Encode(tag, value, options);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"{field.Name}: {ex.Message}", ex);
        }
    }

    private static RecordFieldResponse ResolveField(RecordTypeResponse type, string field)
    {
        var query = field?.Trim() ?? string.Empty;
        var match = type.Fields.FirstOrDefault(f => string.Equals(f.Name, query, StringComparison.OrdinalIgnoreCase))
            ?? (int.TryParse(query, out var tagId) ? type.Fields.FirstOrDefault(f => f.TagId == tagId) : null);

        return match ?? throw new ArgumentException(
            $"'{type.Name}' has no field '{field}'. Its fields: {string.Join(", ", type.Fields.Select(f => f.Name))}.");
    }

    private async Task<DateTime> ParseOrNow(string? dateTime)
    {
        return dateTime == null
            ? await clock.Now(user.UserId)
            : DateArguments.ParseDateTime(dateTime, "dateTime", await clock.Zone(user.UserId));
    }
}
