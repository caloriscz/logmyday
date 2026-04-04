namespace LogMyDay.Shared.DTOs;

public class DashboardDataResponse
{
    public int DashboardId { get; set; }
    public List<PanelDataResponse> Panels { get; set; } = new();
}
