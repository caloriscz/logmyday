using System.Globalization;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Api.Application.Services;

/// <summary>Names and values of record fields. A field is a tag named "{record type}: {field}", so
/// charts, statistics, rules and backups, which know tags by name, tell two record types' "Heart
/// rate" fields apart. Values are stored as the web UI writes them for the field's input type.</summary>
public static class RecordFieldValues
{
    public static string TagName(string recordTypeName, string fieldName) => $"{recordTypeName}: {fieldName}";

    /// <summary>The field's name within its record type.</summary>
    public static string FieldName(string recordTypeName, string tagName)
    {
        var prefix = recordTypeName + ": ";

        return tagName.StartsWith(prefix, StringComparison.Ordinal) ? tagName[prefix.Length..] : tagName;
    }

    /// <summary>A number field can add up or average its values over a day; any other field uses
    /// the latest.</summary>
    public static bool IsNumberField(int? inputTypeId, int? optionListId) =>
        optionListId == null && TagRuleInputTypes.IsNumeric(inputTypeId);

    public static DayCombine DayCombineFor(int? inputTypeId, int? optionListId, DayCombine? requested)
    {
        if (!IsNumberField(inputTypeId, optionListId))
        {
            return DayCombine.Latest;
        }

        return requested is DayCombine combine && Enum.IsDefined(combine) ? combine : DayCombine.Average;
    }

    /// <summary>The value as stored for the field, or null when it is empty (an empty value is
    /// not stored). Throws <see cref="ArgumentException"/> with a message naming the field.</summary>
    public static string? Normalize(Tag field, string fieldName, string? value, IReadOnlyCollection<TagOption>? options)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (field.OptionListId != null && options != null)
        {
            var option = options.FirstOrDefault(o => string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"'{text}' is not an option of {fieldName}.");

            return option.Value;
        }

        switch (field.InputTypeId)
        {
            case InputTypeIds.Integer:
            case InputTypeIds.StarRating:
            case InputTypeIds.StarRating10:
            case InputTypeIds.Percentage:
            case InputTypeIds.Score:
            case InputTypeIds.Score10:
                var whole = ReadNumber(fieldName, text);
                if (Math.Abs(whole - Math.Round(whole)) > 1e-9)
                {
                    throw new ArgumentException($"{fieldName} must be a whole number.");
                }

                EnsureWithinLimits(field, fieldName, whole);

                return Math.Round(whole).ToString(CultureInfo.InvariantCulture);
            case InputTypeIds.Decimal:
                var number = ReadNumber(fieldName, text);
                EnsureWithinLimits(field, fieldName, number);

                return Math.Round(number, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture);
            case InputTypeIds.Boolean:
                return ActivityValueCodec.TryReadYesNo(text, out var yes)
                    ? (yes ? "true" : "false")
                    : throw new ArgumentException($"{fieldName} must be yes or no.");
            case InputTypeIds.Date:
                return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : throw new ArgumentException($"{fieldName} must be a date (yyyy-MM-dd).");
            case InputTypeIds.Time:
                return TimeOnly.TryParseExact(text, ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                    ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
                    : throw new ArgumentException($"{fieldName} must be a time (HH:mm).");
            default:
                return text;
        }
    }

    private static double ReadNumber(string fieldName, string text)
    {
        return ActivityValueCodec.TryReadNumber(text, out var number) && double.IsFinite(number)
            ? number
            : throw new ArgumentException($"{fieldName} must be a number.");
    }

    private static void EnsureWithinLimits(Tag field, string fieldName, double value)
    {
        if (field.MinValue.HasValue && value < field.MinValue.Value)
        {
            throw new ArgumentException($"{fieldName} must be at least {field.MinValue.Value.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (field.MaxValue.HasValue && value > field.MaxValue.Value)
        {
            throw new ArgumentException($"{fieldName} must be at most {field.MaxValue.Value.ToString(CultureInfo.InvariantCulture)}.");
        }
    }
}
