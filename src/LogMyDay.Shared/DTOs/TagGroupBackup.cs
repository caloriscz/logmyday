namespace LogMyDay.Shared.DTOs;

public class TagGroupBackup
{
    public string Name { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
}
