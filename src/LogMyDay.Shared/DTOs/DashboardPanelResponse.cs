namespace LogMyDay.Shared.DTOs;

public class DashboardPanelResponse
{
    public int Id { get; set; }
    public int PanelTypeId { get; set; }
    public int? TagId { get; set; }
    public string? TagName { get; set; }
    public string? TagUnitSymbol { get; set; }
    public int? InputTypeId { get; set; }
    public int AggregationTypeId { get; set; }
    public int TimeRangeId { get; set; }
    public int SizeId { get; set; }
    public int DisplayOrder { get; set; }
    public string? Title { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}
