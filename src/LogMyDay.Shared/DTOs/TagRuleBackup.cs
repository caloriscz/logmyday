namespace LogMyDay.Shared.DTOs;

/// <summary>A Tag Activity Relations rule in a backup. Tags are referenced by name, like reminder
/// completion tags. The values a rule calculated are not in the backup: they are recomputed from
/// the restored source values after the import.</summary>
public class TagRuleBackup
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Absent in backups made before phase 2, which only had weighted sums.</summary>
    public LogMyDay.Domain.Enums.TagRuleTemplate Template { get; set; } = LogMyDay.Domain.Enums.TagRuleTemplate.WeightedSum;
    public LogMyDay.Domain.Enums.TagRuleAggregateKind? AggregateKind { get; set; }

    public string TargetTagName { get; set; } = string.Empty;
    public bool IgnoreZero { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateTime DateCreated { get; set; }
    public List<TagRuleSourceBackup> Sources { get; set; } = new();

    /// <summary>For a conditional rule: the ordered cases, with results as stored values.</summary>
    public List<TagRuleCaseBackup> Cases { get; set; } = new();

    /// <summary>Only for a paused rule: its values as they stand. A paused rule keeps its values
    /// but no longer follows its sources, so they cannot be recomputed after a restore; they are
    /// carried over as they are. Empty for an active rule, whose values are recomputed.</summary>
    public List<TagRuleValueBackup> Values { get; set; } = new();
}

public class TagRuleCaseBackup
{
    public LogMyDay.Domain.Enums.TagRuleOperator Operator { get; set; }
    public string? Operand { get; set; }
    public string ResultValue { get; set; } = string.Empty;
}

public class TagRuleValueBackup
{
    public DateOnly Date { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class TagRuleSourceBackup
{
    public string SourceTagName { get; set; } = string.Empty;
    public double Factor { get; set; } = 1;
}
