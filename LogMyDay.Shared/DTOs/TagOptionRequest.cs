namespace LogMyDay.Shared.DTOs;

public class TagOptionRequest
{
    public int? Id { get; set; }

    public required string Value { get; set; }

    public string? DisplayName { get; set; }
}
