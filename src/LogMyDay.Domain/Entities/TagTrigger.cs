using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

/// <summary>A Tag Activity Relations trigger: "when the If tag meets its condition on a day, remind
/// me to log the Prompt tag". It writes no values; it creates one reminder per day the condition
/// holds (<see cref="Reminder.TriggerId"/>, <see cref="Reminder.TriggerDay"/>), withdraws it when
/// the condition stops holding, and closes it when the Prompt tag is logged.</summary>
public class TagTrigger
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public bool IsEnabled { get; set; } = true;

    public int IfTagId { get; set; }
    public Tag? IfTag { get; set; }
    public TagRuleOperator IfOperator { get; set; } = TagRuleOperator.IsLogged;

    /// <summary>The value compared with; set for =, ≠, &gt;, ≥, &lt;, ≤.</summary>
    public string? IfOperand { get; set; }

    /// <summary>What the reminder asks to log: a tag or a record type.</summary>
    public int PromptTagId { get; set; }
    public Tag? PromptTag { get; set; }

    public TagTriggerTiming Timing { get; set; } = TagTriggerTiming.AfterEntry;

    /// <summary>Minutes after the entry, for <see cref="TagTriggerTiming.AfterEntry"/>.</summary>
    public int? OffsetMinutes { get; set; }

    /// <summary>Time of day, for <see cref="TagTriggerTiming.AtTime"/>.</summary>
    public TimeOnly? AtTime { get; set; }

    public DateTime DateCreated { get; set; }
    public DateTime DateUpdated { get; set; }
}
