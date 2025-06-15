namespace LogMyDay.Shared.DTOs;

public class ActivityResponse
{
    public int Id { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateStarted { get; set; }
    public DateTime? DateFinished { get; set; }
    public string? Description { get; set; }
    public int? PrimaryTagId { get; set; }
    public string PrimaryTagName { get; set; }
    public string PrimaryTagValue { get; set; }
    public int? ElementId { get; set; }
    public string? ElementName { get; set; }
    public bool TagRequired { get; set; }
}
