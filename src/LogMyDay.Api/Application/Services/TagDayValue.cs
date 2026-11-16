using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Api.Application.Services;

/// <summary>A tag's value for one local day and the conditions on it. Shared by the conditional
/// rules and the checks, so both read a day the same way.</summary>
public static class TagDayValue
{
    /// <summary>One entry of the tag on the day.</summary>
    public readonly record struct Entry(int Id, DateTime DateStarted, string? Description);

    /// <summary>The day's number (number and scale tags) or text (yes/no, text and option tags).
    /// Both are null when the day has no readable value.</summary>
    public readonly record struct Value(double? Number, string? Text);

    // Day totals and averages carry binary rounding (0.1 + 0.2 is not exactly 0.3), so equality
    // allows a relative tolerance far below any value a person logs.
    private const double Tolerance = 1e-9;

    /// <summary>Whether a tag stores yes/no: a Boolean tag without an option list.</summary>
    public static bool IsYesNo(int? inputTypeId, int? optionListId) =>
        inputTypeId == InputTypeIds.Boolean && optionListId == null;

    /// <summary>The day value: the total of a number tag, the average of a rating, score or
    /// percentage, and the latest entry otherwise (an option-list tag by its option). Entries at
    /// the same moment resolve by id, so a recompute always picks the same one.</summary>
    public static Value Read(TagRuleValueKind kind, IReadOnlyCollection<Entry> entries)
    {
        if (entries.Count == 0)
        {
            return default;
        }

        if (kind is TagRuleValueKind.Number or TagRuleValueKind.Scale)
        {
            var numbers = entries
                .Select(e => ActivityValueCodec.TryReadNumber(e.Description, out var n) ? (double?)n : null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();
            if (numbers.Count == 0)
            {
                return default;
            }

            return new Value(kind == TagRuleValueKind.Scale ? numbers.Average() : numbers.Sum(), null);
        }

        return new Value(null, entries.OrderBy(e => e.DateStarted).ThenBy(e => e.Id).Last().Description?.Trim());
    }

    /// <summary>Whether the condition holds on a logged day's value. "Is logged" and "otherwise"
    /// always hold here: the caller decides whether the day counts as logged.</summary>
    public static bool Holds(TagRuleOperator op, string? operand, TagRuleValueKind kind, Value day)
    {
        switch (op)
        {
            case TagRuleOperator.IsLogged:
            case TagRuleOperator.Otherwise:
                return true;
            case TagRuleOperator.IsNotLogged:
                return false;
            case TagRuleOperator.IsYes:
                return ActivityValueCodec.TryReadYesNo(day.Text, out var yes) && yes;
            case TagRuleOperator.IsNo:
                return ActivityValueCodec.TryReadYesNo(day.Text, out var no) && !no;
        }

        if (kind is TagRuleValueKind.Number or TagRuleValueKind.Scale)
        {
            if (day.Number is not double value || !ActivityValueCodec.TryReadNumber(operand, out var compared))
            {
                return false;
            }

            var equal = Math.Abs(value - compared) <= Tolerance * Math.Max(1, Math.Max(Math.Abs(value), Math.Abs(compared)));

            return op switch
            {
                TagRuleOperator.Equal => equal,
                TagRuleOperator.NotEqual => !equal,
                TagRuleOperator.Greater => !equal && value > compared,
                TagRuleOperator.GreaterOrEqual => equal || value > compared,
                TagRuleOperator.Less => !equal && value < compared,
                TagRuleOperator.LessOrEqual => equal || value < compared,
                _ => false
            };
        }

        var matches = string.Equals(day.Text, operand?.Trim(), StringComparison.OrdinalIgnoreCase);

        return op switch
        {
            TagRuleOperator.Equal => matches,
            TagRuleOperator.NotEqual => !matches,
            _ => false
        };
    }

    /// <summary>A skip marker, as reminder and todo skips write: 0, or "no" on a yes/no tag.</summary>
    public static bool IsSkipMarker(string? description, bool yesNoTag)
    {
        if (yesNoTag)
        {
            return ActivityValueCodec.TryReadYesNo(description, out var yes) && !yes;
        }

        return ActivityValueCodec.TryReadNumber(description, out var number) && number == 0;
    }
}
