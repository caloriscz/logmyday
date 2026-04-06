namespace LogMyDay.Shared.DTOs;

public class DashboardPanelResponse
{
    public int Id { get; set; }
    public int WidgetTypeId { get; set; }
    public string? WidgetTypeName { get; set; }
    public int? TagId { get; set; }
    public string? TagName { get; set; }
    public string? TagUnitSymbol { get; set; }
    public int? InputTypeId { get; set; }
    public string? Parameters { get; set; }
    public int SizeId { get; set; }
    public int DisplayOrder { get; set; }
    public string? Title { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}
