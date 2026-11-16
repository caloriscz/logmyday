using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

/// <summary>A Tag Activity Relations check in a backup. Tags are referenced by name, like rule
/// sources; compared values are stored values (an option by its value).</summary>
public class TagCheckBackup
{
    public string Name { get; set; } = string.Empty;
    public TagCheckSeverity Severity { get; set; } = TagCheckSeverity.Flag;
    public bool IsEnabled { get; set; } = true;
    public string IfTagName { get; set; } = string.Empty;
    public TagRuleOperator IfOperator { get; set; } = TagRuleOperator.IsLogged;
    public string? IfOperand { get; set; }
    public string ThenTagName { get; set; } = string.Empty;
    public TagRuleOperator ThenOperator { get; set; } = TagRuleOperator.IsLogged;
    public string? ThenOperand { get; set; }
    public DateTime DateCreated { get; set; }
}
