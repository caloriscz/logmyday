using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

/// <summary>A Tag Activity Relations rule. Weighted sum: the target tag gets, per local day, the
/// sum of each source tag's values multiplied by its factor. Aggregate: the average, minimum or
/// maximum of the sources' values, or the count of their entries (factors are not used).</summary>
public class TagRuleRequest
{
    public required string Name { get; set; }
    public TagRuleTemplate Template { get; set; } = TagRuleTemplate.WeightedSum;

    /// <summary>Required for <see cref="TagRuleTemplate.Aggregate"/>.</summary>
    public TagRuleAggregateKind? AggregateKind { get; set; }

    public int TargetTagId { get; set; }
    public bool IgnoreZero { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public List<TagRuleSourceRequest> Sources { get; set; } = new();
}

public class TagRuleSourceRequest
{
    public int SourceTagId { get; set; }
    public double Factor { get; set; } = 1;
}
