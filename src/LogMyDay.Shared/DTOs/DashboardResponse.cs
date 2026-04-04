namespace LogMyDay.Shared.DTOs;

public class DashboardResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime DateCreated { get; set; }
    public List<DashboardPanelResponse> Panels { get; set; } = new();
}
