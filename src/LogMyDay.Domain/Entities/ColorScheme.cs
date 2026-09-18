namespace LogMyDay.Domain.Entities;

public class ColorScheme
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public Guid UserId { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }

    public ICollection<ColorSchemeEntry> Entries { get; set; } = new List<ColorSchemeEntry>();
}
