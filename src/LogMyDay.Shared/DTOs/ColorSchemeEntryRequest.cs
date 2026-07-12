namespace LogMyDay.Shared.DTOs;

public class ColorSchemeEntryRequest
{
    public double? RangeFrom { get; set; }
    public double? RangeTo { get; set; }
    public required string Color { get; set; }
    public int SortOrder { get; set; }
    public string? Label { get; set; }
}
