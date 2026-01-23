using ApexCharts;

namespace LogMyDay.App.Services.Charts;

/// <summary>
/// Represents user preferences for chart configuration.
/// </summary>
public sealed record ChartPreferences(
    int? Tag1Id,
    int? Tag2Id,
    int? Tag3Id,
    string ChartType1,
    string ChartType2,
    string ChartType3,
    string DateRange)
{
    public static ChartPreferences Default => new(
        null, null, null,
        "line", "line", "line",
        "all");
}

/// <summary>
/// Represents a tag selection with its chart type for loading data.
/// </summary>
public sealed record TagSelection(int TagId, string TagTitle, int InputTypeId, string ChartType);

/// <summary>
/// Represents a data series for chart rendering.
/// </summary>
public sealed class ChartSeriesData
{
    public string TagName { get; init; } = string.Empty;
    public int TagId { get; init; }
    public int InputTypeId { get; init; }
    public SeriesType ChartType { get; init; } = SeriesType.Line;
    public List<ChartDataPoint> DataPoints { get; set; } = new();
}

/// <summary>
/// Represents a single data point in a chart series.
/// </summary>
public sealed record ChartDataPoint(decimal Value, DateTime Date);

/// <summary>
/// Result of loading chart data including available date filters.
/// </summary>
public sealed record ChartDataResult(
    List<ChartSeriesData> SeriesData,
    List<int> AvailableYears,
    Dictionary<string, string> AvailableMonths);

/// <summary>
/// Statistics computed from chart data points.
/// </summary>
public sealed record ChartStatistics(
    decimal Minimum,
    decimal Maximum,
    decimal Average,
    int TotalPoints)
{
    public static ChartStatistics FromDataPoints(IEnumerable<ChartDataPoint> points)
    {
        var list = points.ToList();
        if (!list.Any())
        {
            return new ChartStatistics(0, 0, 0, 0);
        }

        return new ChartStatistics(
            list.Min(p => p.Value),
            list.Max(p => p.Value),
            list.Average(p => p.Value),
            list.Count);
    }
}
