using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.UI.Formatting;

/// <summary>Turns a saved check back into the editor's model, shared by the web and mobile pages.</summary>
public static class TagCheckForm
{
    /// <summary>Shown on the Edit button of a check this app version cannot edit.</summary>
    public const string NotEditableHint = "This check uses a test this app version does not know. Update the app to edit it.";

    /// <summary>The editor model for a saved check, or null when it uses a severity or test this
    /// app version does not know. Editing it would silently replace the unknown part.</summary>
    public static TagCheckRequest? ToRequest(TagCheckResponse check)
    {
        if (!TryParse<TagCheckSeverity>(check.Severity, out var severity)
            || !TryParse<TagRuleOperator>(check.IfOperator, out var ifOperator)
            || !TryParse<TagRuleOperator>(check.ThenOperator, out var thenOperator))
        {
            return null;
        }

        return new TagCheckRequest
        {
            Name = check.Name,
            Severity = severity,
            IsEnabled = check.IsEnabled,
            IfTagId = check.IfTagId,
            IfOperator = ifOperator,
            IfOperand = check.IfOperand,
            ThenTagId = check.ThenTagId,
            ThenOperator = thenOperator,
            ThenOperand = check.ThenOperand
        };
    }

    // Enum.TryParse also accepts any number, so a value must be a defined member too.
    private static bool TryParse<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        return Enum.TryParse(text, out value) && Enum.IsDefined(value);
    }
}
