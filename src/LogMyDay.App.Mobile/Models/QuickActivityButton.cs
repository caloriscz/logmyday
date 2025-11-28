namespace LogMyDay.App.Mobile.Models;

public class QuickActivityButton
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsed { get; set; }
    public bool IsEnabled { get; set; } = true;
}
