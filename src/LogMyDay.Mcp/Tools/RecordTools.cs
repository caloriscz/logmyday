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
    UserClock clock)
{
    public sealed record FieldInput(
        [property: Description("Field name within the record type, e.g. \"Systolic\"; its tag is named \"{record type}: {field}\".")] string Name,
        [property: Description("Input type id, as for create_tag.")] int InputTypeId,
        [property: Description("Unit id (units are global).")] int? UnitId = null,
        [property: Description("Option list id; the value must then be one of its options.")] int? OptionListId = null,
        [property: Description("Minimum for numeric types.")] double? MinValue = null,
        [property: Description("Maximum for numeric types.")] double? MaxValue = null,
        [property: Description("Step for numeric types.")] double? Step = null,
        [property: Description("Optional description.")] string? Description = null);

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
                TypeId = f.InputTypeId,
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
    [Description("Logs one record: the values of a record type's fields measured together at one moment. Each value is encoded for its field's input type as in log_value; fields left out or null stay empty, and at least one value is needed. Fails with conflict for a record type logged once per day that already has a record that day.")]
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
            var tag = await tags.GetTagById(field.TagId, user.UserId);
            var options = tag.OptionListId is int listId ? await optionLists.GetById(listId, user.UserId) : null;
            request.Values.Add(new RecordValueRequest { TagId = field.TagId, Value = ValueEncoder.Encode(tag, value.Value, options) });
        }

        return await records.Create(request, user.UserId);
    }

    [McpServerTool(Name = "get_record", Title = "Get record", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one record with its time and its stored values in field order.")]
    public Task<RecordResponse> GetRecord([Description("Record id.")] int recordId) => records.GetById(recordId, user.UserId);

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
