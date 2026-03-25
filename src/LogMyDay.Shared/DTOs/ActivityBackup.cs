namespace LogMyDay.Shared.DTOs;

public class ActivityBackup
{
    public DateTime DateCreated { get; set; }
    public DateTime DateStarted { get; set; }
    public DateTime? DateFinished { get; set; }
    public DateTime? StreakEndDate { get; set; }
    public string? Description { get; set; }
    public string TagName { get; set; } = string.Empty;
}
