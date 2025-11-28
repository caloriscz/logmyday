namespace LogMyDay.Shared.DTOs;

public class TagOptionListResponse
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public bool IsGlobal { get; set; }

    public List<TagOptionResponse> Options { get; set; } = new();
}
