using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TagResponse
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int? InputTypeId { get; set; }
    public int? TypeId { get; set; } // Add this property for tag type
    public bool IsRequired { get; set; } // Added for required column
    public bool IsRepeatable { get; set; }
    public TimeGranularity TimeGranularity { get; set; }
    public bool IsRange { get; set; }
    public int? UnitId { get; set; }
    public string? UnitSymbol { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? Step { get; set; }
    public string? DefaultValue { get; set; }
    public int? OptionListId { get; set; }
    public string? OptionListName { get; set; }
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }
}
