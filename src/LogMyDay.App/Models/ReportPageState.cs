using LogMyDay.Shared.Enums;

namespace LogMyDay.App.Models;

public class ReportPageState
{
    public List<int> ExcelSelectedTagIds { get; set; } = new();
    public List<int> HtmlSelectedTagIds { get; set; } = new();

    public DateTime? ExcelStartDate { get; set; }
    public DateTime? ExcelEndDate { get; set; }
    public bool ExcelIncludeAllData { get; set; }
    public ReportDatePreset ExcelPreset { get; set; } = ReportDatePreset.Custom;
    public bool FreezeFirstRow { get; set; } = true;
    public bool IsGeneratingExcel { get; set; }

    public DateTime? HtmlStartDate { get; set; }
    public DateTime? HtmlEndDate { get; set; }
    public bool HtmlIncludeAllData { get; set; }
    public ReportDatePreset HtmlPreset { get; set; } = ReportDatePreset.Custom;
    public HtmlExportFormat HtmlFormat { get; set; } = HtmlExportFormat.List;
    public bool IsGeneratingHtml { get; set; }

    public string StatusMessage { get; set; } = "";
    public bool IsError { get; set; }
    public List<int> GeneratedYears { get; set; } = new();

    public ReportPageState()
    {
        var today = DateTime.Today;
        ExcelStartDate = today.AddMonths(-1);
        ExcelEndDate = today;
        HtmlStartDate = today.AddMonths(-1);
        HtmlEndDate = today;
    }

    public void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }

    public void ClearStatus()
    {
        StatusMessage = "";
        IsError = false;
    }

    public void ToggleExcelTag(int tagId, bool isSelected)
    {
        if (isSelected)
        {
            if (!ExcelSelectedTagIds.Contains(tagId))
            {
                ExcelSelectedTagIds.Add(tagId);
            }
        }
        else
        {
            ExcelSelectedTagIds.Remove(tagId);
        }
    }

    public void ToggleHtmlTag(int tagId, bool isSelected)
    {
        if (isSelected)
        {
            if (!HtmlSelectedTagIds.Contains(tagId))
            {
                HtmlSelectedTagIds.Add(tagId);
            }
        }
        else
        {
            HtmlSelectedTagIds.Remove(tagId);
        }
    }

    public (DateTime? Start, DateTime? End, string Display) CalculateDateRange(ReportDatePreset preset)
    {
        var today = DateTime.Today;
        
        return preset switch
        {
            ReportDatePreset.Custom => (today.AddMonths(-1), today, ""),
            ReportDatePreset.LastMonth => (today.AddMonths(-1), today, $"{today.AddMonths(-1):dd/MM/yyyy} - {today:dd/MM/yyyy} (Last Month)"),
            ReportDatePreset.LastQuarter => (today.AddMonths(-3), today, $"{today.AddMonths(-3):dd/MM/yyyy} - {today:dd/MM/yyyy} (Last Quarter)"),
            ReportDatePreset.LastYear => (today.AddYears(-1), today, $"{today.AddYears(-1):dd/MM/yyyy} - {today:dd/MM/yyyy} (Last Year)"),
            _ => (today.AddMonths(-1), today, "")
        };
    }
}

public enum HtmlExportFormat
{
    List,
    Table
}
