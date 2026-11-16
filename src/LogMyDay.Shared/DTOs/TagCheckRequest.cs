using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

/// <summary>A check: what should hold on a day, "if the If tag meets its condition, then the
/// Then tag meets its expectation". "Not on the same day" is If <see cref="TagRuleOperator.IsLogged"/>
/// ⇒ Then <see cref="TagRuleOperator.IsNotLogged"/>; "Requires" is If IsLogged ⇒ Then IsLogged.</summary>
public class TagCheckRequest
{
    public required string Name { get; set; }
    public TagCheckSeverity Severity { get; set; } = TagCheckSeverity.Flag;
    public bool IsEnabled { get; set; } = true;

    public int IfTagId { get; set; }

    /// <summary>IsLogged, or a comparison of the day value (=, ≠, &gt;, ≥, &lt;, ≤, is yes, is no).</summary>
    public TagRuleOperator IfOperator { get; set; } = TagRuleOperator.IsLogged;

    /// <summary>The value compared with; required for =, ≠, &gt;, ≥, &lt;, ≤.</summary>
    public string? IfOperand { get; set; }

    public int ThenTagId { get; set; }

    /// <summary>IsLogged, IsNotLogged, or a comparison of the day value.</summary>
    public TagRuleOperator ThenOperator { get; set; } = TagRuleOperator.IsLogged;

    public string? ThenOperand { get; set; }
}
