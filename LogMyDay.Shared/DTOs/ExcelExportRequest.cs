namespace LogMyDay.Shared.DTOs;

public class ExcelExportRequest
{
    public List<int> TagIds { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? UserId { get; set; }
    public ExcelFormat Format { get; set; } = ExcelFormat.Daily;
}

public enum ExcelFormat
{
    Daily,
    Weekly,
    Monthly
}

public class ExcelExportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public byte[]? FileContent { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ExcelExportStatistics Statistics { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ExcelExportStatistics
{
    public int TotalDays { get; set; }
    public int TotalActivities { get; set; }
    public int SelectedTags { get; set; }
    public DateTime? DateRangeStart { get; set; }
    public DateTime? DateRangeEnd { get; set; }
    public Dictionary<string, int> TagActivityCounts { get; set; } = new();
}
