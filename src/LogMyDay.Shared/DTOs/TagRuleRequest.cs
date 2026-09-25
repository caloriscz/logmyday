namespace LogMyDay.Shared.DTOs;

/// <summary>A weighted-sum Tag Activity Relations rule: the target tag gets, per local day,
/// the sum of each source tag's values multiplied by its factor.</summary>
public class TagRuleRequest
{
    public required string Name { get; set; }
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
