namespace LogMyDay.Shared.DTOs;

public class TagOptionListRequest
{
    public required string Name { get; set; }


    public List<TagOptionRequest> Options { get; set; } = new();
}
