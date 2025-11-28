namespace LogMyDay.Shared.DTOs;

public class TagOptionResponse
{
    public int Id { get; set; }

    public required string Value { get; set; }

    public string? DisplayName { get; set; }
}
