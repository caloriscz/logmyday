namespace LogMyDay.Shared.DTOs;

public class PanelDataResponse
{
    public int PanelId { get; set; }
    public int PanelTypeId { get; set; }
    public string? DisplayValue { get; set; }
    public decimal? NumericValue { get; set; }
    public string? TagName { get; set; }
    public string? UnitSymbol { get; set; }
    public bool HasData { get; set; }
}
