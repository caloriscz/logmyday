using System.Globalization;
using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Helpers;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// Turns the JSON scalar an agent sends into the string LogMyDay stores for the tag's input type,
/// exactly as the web UI would have written it: invariant numbers, "true"/"false", yyyy-MM-dd,
/// HH:mm, an option's stored value. Rejects with a message the agent can act on. Tag-level
/// min/max are left to the activity service, which knows the accumulation semantics.
/// </summary>
public static class ValueEncoder
{
    public static string Encode(TagResponse tag, JsonElement value, TagOptionListResponse? optionList = null)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new ArgumentException("A value is required.");
        }

        if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            throw new ArgumentException("The value must be a JSON scalar (number, string or boolean).");
        }

        if (tag.OptionListId != null && optionList != null)
        {
            return EncodeOption(Text(value), optionList);
        }

        return tag.TypeId switch
        {
            InputTypeIds.Integer => Integer(value, null, null),
            InputTypeIds.Decimal => Decimal(value),
            InputTypeIds.Boolean => Boolean(value),
            InputTypeIds.Date => Date(Text(value)),
            InputTypeIds.Time => Time(Text(value)),
            InputTypeIds.StarRating or InputTypeIds.StarRating10 or InputTypeIds.Percentage
                or InputTypeIds.Score or InputTypeIds.Score10 => Ranged(tag.TypeId.Value, value),
            _ => Text(value)
        };
    }

    /// <summary>One line per input type for server_info and the input-types resource.</summary>
    public static string Describe(int? inputTypeId)
    {
        return inputTypeId switch
        {
            InputTypeIds.Integer => "whole number (invariant, e.g. 12 or -3)",
            InputTypeIds.Decimal => "decimal number, stored with two decimals (e.g. 72.5 → \"72.50\")",
            InputTypeIds.Boolean => "boolean; accepts true/false, yes/no, 1/0; stored as \"true\"/\"false\"",
            InputTypeIds.Date => "date as yyyy-MM-dd",
            InputTypeIds.Time => "time of day as HH:mm (24-hour)",
            InputTypeIds.StarRating => "integer 0–5",
            InputTypeIds.StarRating10 => "integer 0–10",
            InputTypeIds.Percentage => "integer 0–100",
            InputTypeIds.Score => "integer 0–5",
            InputTypeIds.Score10 => "integer 0–10",
            _ => "free text; when the tag has an option list, one of its option values"
        };
    }

    private static string Text(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!.Trim(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };
    }

    private static string Integer(JsonElement value, double? min, double? max)
    {
        long number;
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt64(out number))
            {
                throw new ArgumentException($"{value.GetRawText()} is not a whole number.");
            }
        }
        else if (!long.TryParse(Text(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            throw new ArgumentException($"'{Text(value)}' is not a whole number.");
        }

        if ((min.HasValue && number < min) || (max.HasValue && number > max))
        {
            throw new ArgumentException($"{number} is outside the allowed range {min}–{max}.");
        }

        return number.ToString(CultureInfo.InvariantCulture);
    }

    private static string Decimal(JsonElement value)
    {
        double number;
        if (value.ValueKind == JsonValueKind.Number)
        {
            number = value.GetDouble();
        }
        else if (!double.TryParse(Text(value), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            throw new ArgumentException($"'{Text(value)}' is not a number (use a dot as the decimal separator).");
        }

        return Math.Round(number, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string Boolean(JsonElement value)
    {
        var text = Text(value).ToLowerInvariant();

        return text switch
        {
            "true" or "yes" or "y" or "1" or "on" => "true",
            "false" or "no" or "n" or "0" or "off" => "false",
            _ => throw new ArgumentException($"'{Text(value)}' is not a boolean; use true/false, yes/no or 1/0.")
        };
    }

    private static string Date(string text)
    {
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        throw new ArgumentException($"'{text}' is not a date; use yyyy-MM-dd.");
    }

    private static string Time(string text)
    {
        string[] formats = ["HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss"];
        if (TimeOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        throw new ArgumentException($"'{text}' is not a time; use HH:mm (24-hour).");
    }

    private static string Ranged(int inputTypeId, JsonElement value)
    {
        var constraints = InputTypeDefaults.GetConstraintsForType(inputTypeId);

        return Integer(value, constraints.MinValue, constraints.MaxValue);
    }

    private static string EncodeOption(string text, TagOptionListResponse optionList)
    {
        var option = optionList.Options.FirstOrDefault(o =>
            string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase)
            || (o.DisplayName != null && string.Equals(o.DisplayName, text, StringComparison.OrdinalIgnoreCase)));

        if (option == null)
        {
            var allowed = string.Join(", ", optionList.Options.Select(o => $"'{o.Value}'"));

            throw new ArgumentException($"'{text}' is not an option of list '{optionList.Name}'; allowed: {allowed}.");
        }

        return option.Value;
    }
}
