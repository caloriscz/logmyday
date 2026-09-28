using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

/// <summary>One case of a conditional <see cref="TagRule"/>: when <see cref="Operator"/> holds for
/// the source's day value (compared with <see cref="Operand"/>), the target gets
/// <see cref="ResultValue"/>. Cases are tried in <see cref="SortOrder"/>; the first match wins.</summary>
public class TagRuleCase
{
    public int Id { get; set; }

    public int RuleId { get; set; }
    public TagRule? Rule { get; set; }

    public int SortOrder { get; set; }

    public TagRuleOperator Operator { get; set; }

    /// <summary>The value compared with, in the source tag's encoding; null for operators
    /// without one (is logged, otherwise, is yes, is no).</summary>
    public string? Operand { get; set; }

    /// <summary>The result, in the target tag's encoding.</summary>
    public required string ResultValue { get; set; }
}
