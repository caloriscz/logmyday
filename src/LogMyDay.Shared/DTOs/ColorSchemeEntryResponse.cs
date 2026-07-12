using LogMyDay.Domain.Helpers;

namespace LogMyDay.Shared.DTOs;

public class ColorSchemeEntryResponse : IColorEntry
{
    public int Id { get; set; }
    public double? RangeFrom { get; set; }
    public double? RangeTo { get; set; }
    public required string Color { get; set; }
    public int SortOrder { get; set; }
    public string? Label { get; set; }
}
