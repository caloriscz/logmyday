namespace LogMyDay.Shared.DTOs;

public class EventLogResponse
{
    public int Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string? Detail { get; set; }
}
