namespace LogMyDay.Shared.DTOs;

/// <summary>A colour scheme with its entries. Tags and record fields name their scheme, so a
/// scheme restores by name, like a tag group.</summary>
public class ColorSchemeBackup
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
    public List<ColorSchemeEntryBackup> Entries { get; set; } = new();
}

public class ColorSchemeEntryBackup
{
    public double? RangeFrom { get; set; }
    public double? RangeTo { get; set; }
    public string Color { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Label { get; set; }
}
