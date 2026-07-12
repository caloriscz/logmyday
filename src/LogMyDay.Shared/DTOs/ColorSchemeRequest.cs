namespace LogMyDay.Shared.DTOs;

public class ColorSchemeRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public List<ColorSchemeEntryRequest> Entries { get; set; } = new();
}
