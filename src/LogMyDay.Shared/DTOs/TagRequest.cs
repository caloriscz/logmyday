using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TagRequest
{
    public required string Tag { get; set; }
    public string? Description { get; set; }
    public int TypeId { get; set; }
    public bool IsRequired { get; set; } // Added for required checkbox
    public bool IsRepeatable { get; set; } = true;
    public TimeGranularity TimeGranularity { get; set; } = TimeGranularity.Daily;
    public bool IsRange { get; set; } = false;

    public int? UnitId { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? Step { get; set; }
    public string? DefaultValue { get; set; }
    public int? OptionListId { get; set; }
    public int? GroupId { get; set; }
    public int? ColorSchemeId { get; set; }
}
