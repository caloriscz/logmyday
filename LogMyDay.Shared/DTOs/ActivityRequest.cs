namespace LogMyDay.Shared.DTOs;

public class ActivityRequest
{
    public string? Description { get; set; }
    public DateTime DateStarted { get; set; }
    public DateTime? DateFinished { get; set; }
    public int? PrimaryTagId { get; set; }
}
