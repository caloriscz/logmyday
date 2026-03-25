using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TagBackup
{
    public string TagName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? InputTypeName { get; set; }
    public bool IsRequired { get; set; }
    public TimeGranularity TimeGranularity { get; set; }
    public bool IsRepeatable { get; set; }
    public bool IsRange { get; set; }
    public string? PatternName { get; set; }
    public string? UnitKey { get; set; }
    public string? UnitSymbol { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? Step { get; set; }
    public string? DefaultValue { get; set; }
    public string? OptionListKey { get; set; }
    public string? GroupName { get; set; }
}
