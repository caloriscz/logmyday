namespace LogMyDay.App.Mobile.Models;

public class QuickActivityButton
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int TagId { get; set; }
    public required string TagName { get; set; }
    public string? Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsed { get; set; }
    public bool IsEnabled { get; set; } = true;
}
