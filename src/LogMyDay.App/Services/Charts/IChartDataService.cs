using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Services.Charts;

/// <summary>
/// Service for loading and processing chart data.
/// </summary>
public interface IChartDataService
{
    /// <summary>
    /// Loads numeric tags that can be charted.
    /// </summary>
    Task<(List<TagResponse> Tags, Dictionary<int, string> InputTypeNames)> GetNumericTagsAsync();

    /// <summary>
    /// Loads chart data for the selected tags.
    /// </summary>
    Task<ChartDataResult> LoadSeriesDataAsync(
        IEnumerable<TagSelection> selections,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies date range filter to data points.
    /// </summary>
    List<ChartDataPoint> ApplyDateFilter(List<ChartDataPoint> data, string dateRange);

    /// <summary>
    /// Gets Y-axis range based on input type IDs.
    /// </summary>
    (decimal? Min, decimal? Max) GetYAxisRange(IEnumerable<int> inputTypeIds);

    /// <summary>
    /// Generates correlation insights for multi-tag comparison.
    /// </summary>
    List<string> GenerateCorrelationInsights(
        List<ChartSeriesData> seriesData,
        Dictionary<int, string> inputTypeNames);
}
