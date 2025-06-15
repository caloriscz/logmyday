using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TagBackup
{
    public string TagName { get; set; } = string.Empty;
    public string? InputTypeName { get; set; }
    public bool IsRequired { get; set; }
    public TimeGranularity TimeGranularity { get; set; }
    public bool IsRepeatable { get; set; }
    public bool IsRange { get; set; }
    public string? PatternName { get; set; }
    public Guid? UserId { get; set; }
}
