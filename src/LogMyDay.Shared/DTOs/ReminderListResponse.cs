namespace LogMyDay.Shared.DTOs;

public class ReminderListResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DisplayOrder { get; set; }
    public bool ShowOnHomepage { get; set; }
    public DateTime DateCreated { get; set; }
    public IList<ReminderResponse> Items { get; set; } = new List<ReminderResponse>();
}
