using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

/// <summary>A trigger: "when the If tag meets its condition on a day, remind me to log the Prompt
/// tag", after a number of minutes or at a time of day.</summary>
public class TagTriggerRequest
{
    public required string Name { get; set; }
    public bool IsEnabled { get; set; } = true;

    public int IfTagId { get; set; }

    /// <summary>IsLogged, or a comparison of the day value (=, ≠, &gt;, ≥, &lt;, ≤, is yes, is no).</summary>
    public TagRuleOperator IfOperator { get; set; } = TagRuleOperator.IsLogged;

    /// <summary>The value compared with; required for =, ≠, &gt;, ≥, &lt;, ≤.</summary>
    public string? IfOperand { get; set; }

    /// <summary>What the reminder asks to log: a tag or a record type.</summary>
    public int PromptTagId { get; set; }

    public TagTriggerTiming Timing { get; set; } = TagTriggerTiming.AfterEntry;

    /// <summary>Minutes after the entry (5–1440), for AfterEntry.</summary>
    public int? OffsetMinutes { get; set; }

    /// <summary>Time of day, for AtTime.</summary>
    public TimeOnly? AtTime { get; set; }
}

public class TagTriggerResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsEnabled { get; set; }

    public int IfTagId { get; set; }
    public string IfTagName { get; set; } = string.Empty;

    /// <summary>IsLogged, Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual, IsYes or IsNo.</summary>
    public string IfOperator { get; set; } = string.Empty;

    public string? IfOperand { get; set; }

    public int PromptTagId { get; set; }
    public string PromptTagName { get; set; } = string.Empty;
    public bool PromptIsRecordType { get; set; }

    /// <summary>AfterEntry or AtTime.</summary>
    public string Timing { get; set; } = "AfterEntry";

    public int? OffsetMinutes { get; set; }
    public TimeOnly? AtTime { get; set; }

    /// <summary>How many of its reminders are still open (not done or skipped) today or later.</summary>
    public int OpenReminderCount { get; set; }

    public DateTime DateCreated { get; set; }
    public DateTime DateUpdated { get; set; }
}

/// <summary>A trigger in a backup. Tags are referenced by name; the reminders it created are not
/// backed up.</summary>
public class TagTriggerBackup
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string IfTagName { get; set; } = string.Empty;
    public TagRuleOperator IfOperator { get; set; } = TagRuleOperator.IsLogged;
    public string? IfOperand { get; set; }
    public string PromptTagName { get; set; } = string.Empty;
    public TagTriggerTiming Timing { get; set; } = TagTriggerTiming.AfterEntry;
    public int? OffsetMinutes { get; set; }
    public TimeOnly? AtTime { get; set; }
    public DateTime DateCreated { get; set; }
}
