namespace LogMyDay.Shared.DTOs;

/// <summary>A Tag Activity Relations rule in a backup. Tags are referenced by name, like reminder
/// completion tags. The values a rule calculated are not in the backup: they are recomputed from
/// the restored source values after the import.</summary>
public class TagRuleBackup
{
    public string Name { get; set; } = string.Empty;
    public string TargetTagName { get; set; } = string.Empty;
    public bool IgnoreZero { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateTime DateCreated { get; set; }
    public List<TagRuleSourceBackup> Sources { get; set; } = new();
}

public class TagRuleSourceBackup
{
    public string SourceTagName { get; set; } = string.Empty;
    public double Factor { get; set; } = 1;
}
