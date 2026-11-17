using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

/// <summary>A Tag Activity Relations check: what should hold on a day, "if the If tag meets its
/// condition, then the Then tag meets its expectation". It writes nothing and owns no tag; it is
/// read in the Rule review and, for <see cref="TagCheckSeverity.Warn"/>, after a save.
/// "Not on the same day" is If is logged ⇒ Then is not logged; "Requires" is If is logged ⇒ Then
/// is logged.</summary>
public class TagCheck
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public TagCheckSeverity Severity { get; set; } = TagCheckSeverity.Flag;
    public bool IsEnabled { get; set; } = true;

    public int IfTagId { get; set; }
    public Tag? IfTag { get; set; }
    public TagRuleOperator IfOperator { get; set; } = TagRuleOperator.IsLogged;

    /// <summary>The value compared with; set for =, ≠, &gt;, ≥, &lt;, ≤.</summary>
    public string? IfOperand { get; set; }

    public int ThenTagId { get; set; }
    public Tag? ThenTag { get; set; }
    public TagRuleOperator ThenOperator { get; set; } = TagRuleOperator.IsLogged;
    public string? ThenOperand { get; set; }

    public DateTime DateCreated { get; set; }
    public DateTime DateUpdated { get; set; }
}
