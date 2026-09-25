namespace LogMyDay.Shared.DTOs;

public class TagRuleResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Template { get; set; } = "WeightedSum";
    public int TargetTagId { get; set; }
    public string TargetTagName { get; set; } = string.Empty;
    public string? TargetUnitSymbol { get; set; }
    public bool IgnoreZero { get; set; }
    public bool IsEnabled { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateUpdated { get; set; }
    public List<TagRuleSourceResponse> Sources { get; set; } = new();
}

public class TagRuleSourceResponse
{
    public int SourceTagId { get; set; }
    public string SourceTagName { get; set; } = string.Empty;
    public string? SourceUnitSymbol { get; set; }
    public double Factor { get; set; }
}
