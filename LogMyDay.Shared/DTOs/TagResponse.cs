using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TagResponse
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public int? InputTypeId { get; set; }
    public int? TypeId { get; set; } // Add this property for tag type
    public bool IsRequired { get; set; } // Added for required column
    public bool IsRepeatable { get; set; }
    public TimeGranularity TimeGranularity { get; set; }
    public bool IsRange { get; set; }
}
