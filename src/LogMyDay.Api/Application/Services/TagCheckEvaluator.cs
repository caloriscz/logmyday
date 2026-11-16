using System.Globalization;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Api.Application.Services;

/// <summary>How a check comes out on one day.</summary>
public enum TagCheckOutcome
{
    /// <summary>The If tag has no entry that day, or its condition does not hold.</summary>
    NotApplicable,

    Met,
    Broken,

    /// <summary>Today, the Then tag is expected but not logged yet: the day is not over.</summary>
    Pending
}

/// <summary>Evaluates a check on one local day from the day's entries of its two tags.</summary>
public static class TagCheckEvaluator
{
    /// <summary>The outcome and the day values found, as stored (numbers invariant, yes/no as
    /// true/false, options by value). A value is null when its tag has no entry that day.</summary>
    public readonly record struct DayResult(TagCheckOutcome Outcome, string? IfValue, string? ThenValue);

    /// <param name="isToday">The day is the user's today. A Then tag that should be logged may
    /// still come later, so it is pending, not broken.</param>
    public static DayResult Evaluate(TagCheck check, Tag ifTag, Tag thenTag,
        IReadOnlyCollection<TagDayValue.Entry> ifEntries, IReadOnlyCollection<TagDayValue.Entry> thenEntries, bool isToday)
    {
        if (ifEntries.Count == 0)
        {
            return new DayResult(TagCheckOutcome.NotApplicable, null, null);
        }

        var (applies, ifValue) = Condition(check.IfOperator, check.IfOperand, ifTag, ifEntries);
        if (!applies)
        {
            return new DayResult(TagCheckOutcome.NotApplicable, ifValue, null);
        }

        var thenKind = TagRuleInputTypes.ConditionKind(thenTag.InputTypeId, thenTag.OptionListId != null);
        var thenDay = TagDayValue.Read(thenKind, thenEntries);
        var thenValue = thenEntries.Count > 0 ? Show(thenDay) : null;

        // A value expectation on a day without the Then tag is met: nothing contradicts it.
        var met = check.ThenOperator switch
        {
            TagRuleOperator.IsLogged => Happened(thenTag, thenEntries),
            TagRuleOperator.IsNotLogged => !Happened(thenTag, thenEntries),
            _ => thenEntries.Count == 0 || TagDayValue.Holds(check.ThenOperator, check.ThenOperand, thenKind, thenDay)
        };

        if (met)
        {
            return new DayResult(TagCheckOutcome.Met, ifValue, thenValue);
        }

        var outcome = check.ThenOperator == TagRuleOperator.IsLogged && isToday ? TagCheckOutcome.Pending : TagCheckOutcome.Broken;

        return new DayResult(outcome, ifValue, thenValue);
    }

    /// <summary>Whether a condition on a tag holds on a day, and the tag's day value as shown.
    /// Shared by a check's If side and a trigger. "Is logged" means it happened: a skip marker (0,
    /// or "no" on a yes/no tag) does not count. A value condition compares the day value of every
    /// entry.</summary>
    public static (bool Holds, string? Value) Condition(TagRuleOperator op, string? operand, Tag tag,
        IReadOnlyCollection<TagDayValue.Entry> entries)
    {
        if (entries.Count == 0)
        {
            return (false, null);
        }

        var kind = TagRuleInputTypes.ConditionKind(tag.InputTypeId, tag.OptionListId != null);
        var day = TagDayValue.Read(kind, entries);
        var holds = op == TagRuleOperator.IsLogged
            ? Happened(tag, entries)
            : TagDayValue.Holds(op, operand, kind, day);

        return (holds, Show(day));
    }

    /// <summary>A real entry: not a skip marker (0, or "no" on a yes/no tag).</summary>
    public static bool IsReal(Tag tag, TagDayValue.Entry entry)
    {
        return !TagDayValue.IsSkipMarker(entry.Description, TagDayValue.IsYesNo(tag.InputTypeId, tag.OptionListId));
    }

    private static bool Happened(Tag tag, IReadOnlyCollection<TagDayValue.Entry> entries)
    {
        var yesNo = TagDayValue.IsYesNo(tag.InputTypeId, tag.OptionListId);

        return entries.Any(e => !TagDayValue.IsSkipMarker(e.Description, yesNo));
    }

    private static string? Show(TagDayValue.Value day)
    {
        return day.Number is double number ? number.ToString("0.##", CultureInfo.InvariantCulture) : day.Text;
    }
}
