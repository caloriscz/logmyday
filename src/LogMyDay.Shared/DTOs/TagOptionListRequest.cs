namespace LogMyDay.Shared.DTOs;

public class TagOptionListRequest
{
    public required string Name { get; set; }

    public bool IsGlobal { get; set; }

    public List<TagOptionRequest> Options { get; set; } = new();
}
