using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Api.Application.Services;

/// <summary>Checks a trigger against the tags it names and normalises its condition's compared
/// value. Shared by the trigger API and backup import.</summary>
public static class TagTriggerValidator
{
    public const int MaxNameLength = 100;
    public const int MinOffsetMinutes = 5;
    public const int MaxOffsetMinutes = 1440;

    /// <param name="ifOptions">The If tag's options when it has an option list.</param>
    /// <returns>The compared value to store (null where the condition compares nothing).</returns>
    /// <exception cref="ArgumentException">The trigger does not fit; the message says why.</exception>
    public static string? Validate(TagRuleOperator ifOperator, string? ifOperand, Tag ifTag, Tag promptTag,
        IReadOnlyList<TagOption>? ifOptions, TagTriggerTiming timing, int? offsetMinutes, TimeOnly? atTime)
    {
        if (ifTag.Id == promptTag.Id)
        {
            throw new ArgumentException("A trigger reminds you to log a different tag than the one it watches.");
        }

        // A record type holds no values; its fields do.
        if (ifTag.IsRecordType)
        {
            throw new ArgumentException($"'{ifTag.TagName}' is a record type and holds no values. Choose one of its fields.");
        }

        // The reminder logs the prompted tag, so it must be one a reminder can complete through.
        if (promptTag.IsComputed)
        {
            throw new ArgumentException($"'{promptTag.TagName}' is computed by a rule, so a reminder cannot ask to log it.");
        }

        if (promptTag.RecordTypeId != null)
        {
            throw new ArgumentException($"'{promptTag.TagName}' is a field of a record type. Remind to log the record type instead.");
        }

        if (!Enum.IsDefined(timing))
        {
            throw new ArgumentException("Unknown timing.");
        }

        if (timing == TagTriggerTiming.AfterEntry
            && (offsetMinutes is not int minutes || minutes < MinOffsetMinutes || minutes > MaxOffsetMinutes))
        {
            throw new ArgumentException($"Remind between {MinOffsetMinutes} minutes and 24 hours after the entry.");
        }

        if (timing == TagTriggerTiming.AtTime && atTime == null)
        {
            throw new ArgumentException("Choose the time of the reminder.");
        }

        return TagCheckValidator.Side("Condition", ifOperator, ifOperand, ifTag, ifOptions, expectation: false);
    }
}
