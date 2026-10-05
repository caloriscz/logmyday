using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.UI.Formatting;

/// <summary>Turns a saved rule back into the editor's model, shared by the web and mobile Rules
/// pages.</summary>
public static class TagRuleForm
{
    /// <summary>Shown on the Edit button of a rule this app version cannot edit.</summary>
    public const string NotEditableHint = "This rule uses a calculation this app version does not know. Update the app to edit it.";

    /// <summary>The editor model for a saved rule, or null when the rule uses a template,
    /// aggregate kind or condition this app version does not know. Editing such a rule would
    /// silently replace the unknown part, so it is not offered.</summary>
    public static TagRuleRequest? ToRequest(TagRuleResponse rule)
    {
        if (!TryParse<TagRuleTemplate>(rule.Template, out var template))
        {
            return null;
        }

        TagRuleAggregateKind? aggregateKind = null;
        if (rule.AggregateKind != null)
        {
            if (!TryParse<TagRuleAggregateKind>(rule.AggregateKind, out var kind))
            {
                return null;
            }

            aggregateKind = kind;
        }

        var cases = new List<TagRuleCaseRequest>();
        foreach (var ruleCase in rule.Cases)
        {
            if (!TryParse<TagRuleOperator>(ruleCase.Operator, out var op))
            {
                return null;
            }

            cases.Add(new TagRuleCaseRequest { Operator = op, Operand = ruleCase.Operand, ResultValue = ruleCase.ResultValue });
        }

        return new TagRuleRequest
        {
            Name = rule.Name,
            Template = template,
            AggregateKind = aggregateKind,
            TargetTagId = rule.TargetTagId,
            IgnoreZero = rule.IgnoreZero,
            IsEnabled = rule.IsEnabled,
            Sources = rule.Sources.Select(s => new TagRuleSourceRequest { SourceTagId = s.SourceTagId, Factor = s.Factor }).ToList(),
            Cases = cases
        };
    }

    // Enum.TryParse also accepts any number, so a value must be a defined member too.
    private static bool TryParse<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        return Enum.TryParse(text, out value) && Enum.IsDefined(value);
    }
}
