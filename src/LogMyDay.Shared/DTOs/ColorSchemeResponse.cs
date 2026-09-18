namespace LogMyDay.Shared.DTOs;

public class ColorSchemeResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
    public List<ColorSchemeEntryResponse> Entries { get; set; } = new();
}
