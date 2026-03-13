namespace LogMyDay.Shared.DTOs;

public class TagGroupRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
}
