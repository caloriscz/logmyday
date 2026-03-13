namespace LogMyDay.Shared.DTOs;

public class TagGroupResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
}
