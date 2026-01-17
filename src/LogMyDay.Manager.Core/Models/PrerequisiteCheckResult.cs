namespace LogMyDay.Manager.Core.Models;

public class PrerequisiteCheckResult
{
    public bool IsSuccess { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
