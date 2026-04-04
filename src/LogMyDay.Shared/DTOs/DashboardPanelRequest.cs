namespace LogMyDay.Shared.DTOs;

public class DashboardPanelRequest
{
    public int PanelTypeId { get; set; }
    public int? TagId { get; set; }
    public int AggregationTypeId { get; set; }
    public int TimeRangeId { get; set; }
    public int SizeId { get; set; }
    public int DisplayOrder { get; set; }
    public string? Title { get; set; }
    public bool IsActive { get; set; } = true;
}
