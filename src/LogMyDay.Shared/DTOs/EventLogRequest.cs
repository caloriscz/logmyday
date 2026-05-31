namespace LogMyDay.Shared.DTOs;

public class EventLogRequest
{
    public string Message { get; set; } = string.Empty;

    public string Level { get; set; } = "Error";
}
