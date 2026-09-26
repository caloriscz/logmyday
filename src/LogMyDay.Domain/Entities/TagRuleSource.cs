namespace LogMyDay.Domain.Entities;

/// <summary>One source tag of a <see cref="TagRule"/> and the factor its daily values are
/// multiplied by. The factor carries any unit conversion and may be negative.</summary>
public class TagRuleSource
{
    public int Id { get; set; }

    public int RuleId { get; set; }
    public TagRule? Rule { get; set; }

    public int SourceTagId { get; set; }
    public Tag? SourceTag { get; set; }

    public double Factor { get; set; } = 1;
}
