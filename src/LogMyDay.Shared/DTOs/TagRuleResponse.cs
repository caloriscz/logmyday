namespace LogMyDay.Shared.DTOs;

public class TagRuleResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    /// <summary>WeightedSum, Aggregate or Conditional.</summary>
    public string Template { get; set; } = "WeightedSum";

    /// <summary>For an aggregate rule: Average, Minimum, Maximum or Count.</summary>
    public string? AggregateKind { get; set; }
    public int TargetTagId { get; set; }
    public string TargetTagName { get; set; } = string.Empty;
    public string? TargetUnitSymbol { get; set; }
    public bool IgnoreZero { get; set; }
    public bool IsEnabled { get; set; }
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>How many calculated values the rule currently has.</summary>
    public int ResultCount { get; set; }
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
