using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Api.Application.Services;

/// <summary>Checks a check's two sides against the tags they name and normalises the compared
/// values to their stored encodings. Shared by the check API and backup import, so a restored
/// check meets the same conditions as one created in the editor.</summary>
public static class TagCheckValidator
{
    public const int MaxNameLength = 100;

    /// <param name="ifOptions">The If tag's options when it has an option list.</param>
    /// <param name="thenOptions">The Then tag's options when it has an option list.</param>
    /// <returns>The compared values to store (null where the test compares nothing).</returns>
    /// <exception cref="ArgumentException">The check does not fit; the message says why.</exception>
    public static (string? IfOperand, string? ThenOperand) Validate(TagCheckSeverity severity,
        TagRuleOperator ifOperator, string? ifOperand, TagRuleOperator thenOperator, string? thenOperand,
        Tag ifTag, Tag thenTag, IReadOnlyList<TagOption>? ifOptions, IReadOnlyList<TagOption>? thenOptions)
    {
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentException("Unknown severity.");
        }

        if (ifTag.Id == thenTag.Id)
        {
            throw new ArgumentException("A check compares two different tags.");
        }

        foreach (var tag in new[] { ifTag, thenTag })
        {
            // A record type holds no values; its fields do.
            if (tag.IsRecordType)
            {
                throw new ArgumentException($"'{tag.TagName}' is a record type and holds no values. Choose one of its fields.");
            }
        }

        return (Side("Condition", ifOperator, ifOperand, ifTag, ifOptions, expectation: false),
            Side("Expectation", thenOperator, thenOperand, thenTag, thenOptions, expectation: true));
    }

    /// <summary>One side of a check, also a trigger's condition: the test fits the tag and the
    /// compared value is normalised to its stored encoding.</summary>
    internal static string? Side(string position, TagRuleOperator op, string? operand, Tag tag, IReadOnlyList<TagOption>? options, bool expectation)
    {
        if (!Enum.IsDefined(op))
        {
            throw new ArgumentException($"{position}: unknown test.");
        }

        var kind = TagRuleInputTypes.ConditionKind(tag.InputTypeId, tag.OptionListId != null);
        if (!TagRuleInputTypes.AllowsInCheck(kind, op, expectation))
        {
            throw new ArgumentException(op switch
            {
                TagRuleOperator.Otherwise => $"{position}: \"otherwise\" is only for rules.",
                TagRuleOperator.IsNotLogged => $"{position}: \"is not logged\" can only be expected of the second tag. A day without the first tag is never checked.",
                TagRuleOperator.IsYes or TagRuleOperator.IsNo => $"{position}: \"is yes\" and \"is no\" need a yes/no tag.",
                TagRuleOperator.Equal or TagRuleOperator.NotEqual => $"{position}: use \"is yes\" or \"is no\" for a yes/no tag.",
                _ => $"{position}: greater and less than need a number, rating, score or percentage tag."
            });
        }

        // Skip markers count as values in a comparison, so "= 0" and "is no" are meaningful here.
        return TagRuleInputTypes.NeedsOperand(op)
            ? TagRuleCaseValidator.NormaliseOperand(operand, op, kind, ignoreZero: false, tag, options, position)
            : null;
    }
}
