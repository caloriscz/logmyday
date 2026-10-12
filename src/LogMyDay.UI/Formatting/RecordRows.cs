using System.Globalization;
using LogMyDay.Domain.Constants;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.UI.Formatting;

/// <summary>Shows records in activity lists: a record's values arrive as one row per field and are
/// shown as one row, e.g. "Blood pressure" with "Systolic 128 · Diastolic 82 · Pulse 64 bpm".
/// Shared by the web and mobile lists.</summary>
public static class RecordRows
{
    /// <summary>Each field tag with its record type and field.</summary>
    public static Dictionary<int, (RecordTypeResponse Type, RecordFieldResponse Field)> FieldIndex(IEnumerable<RecordTypeResponse>? recordTypes)
    {
        var index = new Dictionary<int, (RecordTypeResponse, RecordFieldResponse)>();
        foreach (var type in recordTypes ?? [])
        {
            foreach (var field in type.Fields)
            {
                index[field.TagId] = (type, field);
            }
        }

        return index;
    }

    /// <summary>The list with each record's values replaced by one row, in the place of the
    /// record's first value. The row has no tag (it is not a value of one tag), carries the
    /// record id, its type as the tag name and the values as text. Rows of unknown fields stay
    /// as they are. With the option lists, an option shows its display name.</summary>
    public static List<ActivityResponse> Collapse(IEnumerable<ActivityResponse> items,
        IReadOnlyDictionary<int, (RecordTypeResponse Type, RecordFieldResponse Field)> fields, CultureInfo culture,
        IReadOnlyDictionary<int, TagOptionListResponse>? optionLists = null)
    {
        var list = items.ToList();
        if (fields.Count == 0 || list.All(a => a.RecordId == null))
        {
            return list;
        }

        var byRecord = list
            .Where(a => a.RecordId != null && a.PrimaryTagId is int tagId && fields.ContainsKey(tagId))
            .GroupBy(a => a.RecordId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ActivityResponse>();
        var emitted = new HashSet<int>();
        foreach (var activity in list)
        {
            if (activity.RecordId is not int recordId || !byRecord.TryGetValue(recordId, out var values))
            {
                result.Add(activity);
                continue;
            }

            if (!emitted.Add(recordId))
            {
                continue;
            }

            var type = fields[values[0].PrimaryTagId!.Value].Type;
            var summary = Summary(values.Select(v => (fields[v.PrimaryTagId!.Value].Field, v.Description ?? string.Empty)), culture, optionLists);
            result.Add(new ActivityResponse
            {
                Id = values.Min(v => v.Id),
                RecordId = recordId,
                DateCreated = values[0].DateCreated,
                DateStarted = values[0].DateStarted,
                PrimaryTagId = null,
                PrimaryTagName = Title(type),
                PrimaryTagValue = summary,
                Description = summary,
                ElementName = "Record"
            });
        }

        return result;
    }

    /// <summary>The record type's name as lists show a tag: with its group.</summary>
    public static string Title(RecordTypeResponse type) =>
        type.GroupName != null ? $"{type.GroupName}: {type.Name}" : type.Name;

    /// <summary>"Systolic 128 · Diastolic 82 · Pulse 64 bpm", in field order.</summary>
    public static string Summary(IEnumerable<(RecordFieldResponse Field, string Value)> values, CultureInfo culture,
        IReadOnlyDictionary<int, TagOptionListResponse>? optionLists = null)
    {
        return string.Join(" · ", values
            .OrderBy(v => v.Field.Order)
            .Select(v => $"{v.Field.Name} {FormatValue(v.Field, v.Value, culture, optionLists)}"));
    }

    public static string FormatValue(RecordFieldResponse field, string value, CultureInfo culture,
        IReadOnlyDictionary<int, TagOptionListResponse>? optionLists = null)
    {
        string text;
        if (field.OptionListId is int listId && optionLists != null && optionLists.TryGetValue(listId, out var list)
            && list.Options.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)) is { } option)
        {
            text = option.DisplayName ?? option.Value;
        }
        else if (field.OptionListId == null && TagRuleInputTypes.IsNumeric(field.InputTypeId)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            text = number.ToString("0.##", culture);
        }
        else if (field.InputTypeId == InputTypeIds.Boolean)
        {
            text = value.Trim().ToLowerInvariant() is "true" or "yes" or "1" ? "yes" : "no";
        }
        else
        {
            text = value;
        }

        return string.IsNullOrEmpty(field.UnitSymbol) ? text : $"{text} {field.UnitSymbol}";
    }
}
