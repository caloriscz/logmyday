using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.UI.Formatting;

/// <summary>Turns a saved trigger back into the editor's model, shared by the web and mobile pages.</summary>
public static class TagTriggerForm
{
    /// <summary>Shown on the Edit button of a trigger this app version cannot edit.</summary>
    public const string NotEditableHint = "This trigger uses a test or timing this app version does not know. Update the app to edit it.";

    /// <summary>The editor model for a saved trigger, or null when it uses a test or timing this
    /// app version does not know. Editing it would silently replace the unknown part.</summary>
    public static TagTriggerRequest? ToRequest(TagTriggerResponse trigger)
    {
        if (!TryParse<TagRuleOperator>(trigger.IfOperator, out var ifOperator)
            || !TryParse<TagTriggerTiming>(trigger.Timing, out var timing))
        {
            return null;
        }

        return new TagTriggerRequest
        {
            Name = trigger.Name,
            IsEnabled = trigger.IsEnabled,
            IfTagId = trigger.IfTagId,
            IfOperator = ifOperator,
            IfOperand = trigger.IfOperand,
            PromptTagId = trigger.PromptTagId,
            Timing = timing,
            OffsetMinutes = trigger.OffsetMinutes,
            AtTime = trigger.AtTime
        };
    }

    /// <summary>Record types as entries of a tag picker, so a trigger can prompt for one. Named
    /// "(record)" so they are told apart from tags.</summary>
    public static IEnumerable<TagResponse> RecordTypesAsTags(IEnumerable<RecordTypeResponse> types)
    {
        return types.Select(t => new TagResponse
        {
            Id = t.Id,
            Title = t.GroupName != null ? $"{t.GroupName}: {t.Name} (record)" : $"{t.Name} (record)",
            GroupId = t.GroupId,
            GroupName = t.GroupName,
            IsRecordType = true
        });
    }

    // Enum.TryParse also accepts any number, so a value must be a defined member too.
    private static bool TryParse<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        return Enum.TryParse(text, out value) && Enum.IsDefined(value);
    }
}
