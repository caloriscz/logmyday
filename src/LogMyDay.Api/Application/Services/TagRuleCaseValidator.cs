using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Services;

/// <summary>Checks a conditional rule's cases against its source and result tags and normalises
/// operands and results to the stored encodings. Shared by the rule API and backup import, so a
/// restored rule meets the same conditions as one created in the editor.</summary>
public static class TagRuleCaseValidator
{
    public const int MaxCases = 20;

    /// <param name="sourceOptions">The source tag's options when it has an option list.</param>
    /// <param name="targetOptions">The result tag's options when it has an option list.</param>
    /// <exception cref="ArgumentException">A case does not fit; the message names it.</exception>
    public static List<TagRuleCase> Validate(IReadOnlyList<TagRuleCaseRequest> requests, bool ignoreZero, Tag source, Tag target,
        IReadOnlyList<TagOption>? sourceOptions, IReadOnlyList<TagOption>? targetOptions)
    {
        if (requests.Count == 0)
        {
            throw new ArgumentException("A conditional rule needs at least one case.");
        }

        if (requests.Count > MaxCases)
        {
            throw new ArgumentException($"A rule can have at most {MaxCases} cases.");
        }

        var kind = TagRuleInputTypes.ConditionKind(source.InputTypeId, source.OptionListId != null);
        var cases = new List<TagRuleCase>();
        for (var i = 0; i < requests.Count; i++)
        {
            var requested = requests[i];
            var position = $"Case {i + 1}";
            var op = requested.Operator;

            if (!Enum.IsDefined(op))
            {
                throw new ArgumentException($"{position}: unknown condition.");
            }

            if (op == TagRuleOperator.Otherwise && i != requests.Count - 1)
            {
                throw new ArgumentException($"{position}: \"otherwise\" can only be the last case.");
            }

            if (!TagRuleInputTypes.Allows(kind, op))
            {
                throw new ArgumentException(op switch
                {
                    TagRuleOperator.IsYes or TagRuleOperator.IsNo => $"{position}: \"is yes\" and \"is no\" need a yes/no source tag.",
                    TagRuleOperator.Equal or TagRuleOperator.NotEqual => $"{position}: use \"is yes\" or \"is no\" for a yes/no source tag.",
                    _ => $"{position}: greater and less than need a number, rating, score or percentage source tag."
                });
            }

            if (op == TagRuleOperator.IsNo && ignoreZero)
            {
                throw new ArgumentException($"{position}: \"is no\" cannot match while \"Ignore zero values\" leaves out \"no\" entries. Turn that option off.");
            }

            cases.Add(new TagRuleCase
            {
                SortOrder = i,
                Operator = op,
                Operand = TagRuleInputTypes.NeedsOperand(op)
                    ? NormaliseOperand(requested.Operand, op, kind, ignoreZero, source, sourceOptions, position)
                    : null,
                ResultValue = NormaliseResult(requested.ResultValue, target, targetOptions, position)
            });
        }

        return cases;
    }

    private static string NormaliseOperand(string? value, TagRuleOperator op, TagRuleValueKind kind, bool ignoreZero, Tag source,
        IReadOnlyList<TagOption>? sourceOptions, string position)
    {
        var operand = value?.Trim() ?? string.Empty;
        if (kind is TagRuleValueKind.Number or TagRuleValueKind.Scale)
        {
            if (!ActivityValueCodec.TryReadNumber(operand, out var number))
            {
                throw new ArgumentException($"{position}: enter a number to compare with.");
            }

            if (op == TagRuleOperator.Equal && number == 0 && ignoreZero)
            {
                throw new ArgumentException($"{position}: \"= 0\" cannot match while \"Ignore zero values\" leaves out zero entries. Turn that option off.");
            }

            return operand;
        }

        if (operand.Length == 0)
        {
            throw new ArgumentException($"{position}: enter a value to compare with.");
        }

        if (sourceOptions != null)
        {
            return MatchOption(sourceOptions, operand)?.Value
                ?? throw new ArgumentException($"{position}: '{operand}' is not an option of '{source.TagName}'.");
        }

        return operand.Length <= 500
            ? operand
            : throw new ArgumentException($"{position}: the compared text is longer than 500 characters.");
    }

    private static string NormaliseResult(string? value, Tag target, IReadOnlyList<TagOption>? options, string position)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            throw new ArgumentException($"{position}: enter the value the result tag gets.");
        }

        if (options != null)
        {
            return MatchOption(options, text)?.Value
                ?? throw new ArgumentException($"{position}: '{text}' is not an option of '{target.TagName}'.");
        }

        if (target.InputTypeId == InputTypeIds.Boolean)
        {
            return ActivityValueCodec.TryReadYesNo(text, out var yes)
                ? (yes ? "true" : "false")
                : throw new ArgumentException($"{position}: '{target.TagName}' takes yes or no.");
        }

        if (TagRuleInputTypes.IsNumber(target.InputTypeId))
        {
            return ActivityValueCodec.TryReadNumber(text, out var number)
                ? ActivityValueCodec.WriteNumber(target.InputTypeId, number)
                : throw new ArgumentException($"{position}: '{target.TagName}' takes a number.");
        }

        return text.Length <= 500
            ? text
            : throw new ArgumentException($"{position}: the text is longer than 500 characters.");
    }

    // An option is named by its stored value or its display name.
    private static TagOption? MatchOption(IReadOnlyList<TagOption> options, string text)
    {
        return options.FirstOrDefault(o => string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase)
            || (o.DisplayName != null && string.Equals(o.DisplayName, text, StringComparison.OrdinalIgnoreCase)));
    }
}
