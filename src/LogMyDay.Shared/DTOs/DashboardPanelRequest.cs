namespace LogMyDay.Shared.DTOs;

public class DashboardPanelRequest
{
    public int WidgetTypeId { get; set; }
    public int? TagId { get; set; }
    public string? Parameters { get; set; }
    public int SizeId { get; set; }
    public int DisplayOrder { get; set; }
    public string? Title { get; set; }
    public bool IsActive { get; set; } = true;
}
