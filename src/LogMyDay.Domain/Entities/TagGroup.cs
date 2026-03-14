namespace LogMyDay.Domain.Entities;

public class TagGroup
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public Guid UserId { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
}
