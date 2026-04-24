using System.Globalization;
using ApexCharts;
using LogMyDay.Domain.Constants;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;

namespace LogMyDay.App.Services.Charts;

/// <summary>
/// Service for loading and processing chart data.
/// </summary>
public class ChartDataService : IChartDataService
{
    private readonly IActivityApi _activityApi;

    public ChartDataService(IActivityApi activityApi)
    {
        _activityApi = activityApi;
    }

    public async Task<(List<TagResponse> Tags, Dictionary<int, string> InputTypeNames)> GetNumericTagsAsync()
    {
        var inputTypes = await _activityApi.GetInputTypesAsync();
        var inputTypeNames = inputTypes.ToDictionary(it => it.Id, it => it.Name);

        var numericTypeIds = new HashSet<int?>
        {
            InputTypeIds.Integer,
            InputTypeIds.Decimal,
            InputTypeIds.StarRating,
            InputTypeIds.StarRating10,
            InputTypeIds.Score,
            InputTypeIds.Score10,
            InputTypeIds.Percentage
        };
        // Add Decimal precision 2 by name (no dedicated constant)
        var decimalP2Id = inputTypes.FirstOrDefault(it => it.Name == "Decimal, precision 2")?.Id;
        if (decimalP2Id.HasValue)
        {
            numericTypeIds.Add(decimalP2Id.Value);
        }

        var allTags = await _activityApi.GetTags();
        var numericTags = allTags
            .Where(t => t.InputTypeId.HasValue && numericTypeIds.Contains(t.InputTypeId))
            .OrderBy(t => t.Title)
            .ToList();

        return (numericTags, inputTypeNames);
    }

    public async Task<ChartDataResult> LoadSeriesDataAsync(
        IEnumerable<TagSelection> selections,
        CancellationToken cancellationToken = default)
    {
        var seriesData = new List<ChartSeriesData>();
        var allCombinedPoints = new List<ChartDataPoint>();

        foreach (var selection in selections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _activityApi.GetActivities(
                pageNumber: 1,
                pageSize: ChartConstants.DefaultPageSize,
                orderBy: "asc",
                tagId: selection.TagId);

            var dataPoints = result.Items
                .Where(a => !string.IsNullOrEmpty(a.Description) &&
                           decimal.TryParse(a.Description, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                .Select(a => new ChartDataPoint(
                    decimal.Parse(a.Description!, CultureInfo.InvariantCulture),
                    a.DateStarted.Date))
                .OrderBy(d => d.Date)
                .ToList();

            if (dataPoints.Any())
            {
                var chartType = selection.ChartType == ChartConstants.BarChart
                    ? SeriesType.Bar
                    : SeriesType.Line;

                seriesData.Add(new ChartSeriesData
                {
                    TagName = selection.TagTitle,
                    TagId = selection.TagId,
                    InputTypeId = selection.InputTypeId,
                    ChartType = chartType,
                    DataPoints = dataPoints
                });

                allCombinedPoints.AddRange(dataPoints);
            }
        }

        var availableYears = allCombinedPoints
            .Select(p => p.Date.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        var availableMonths = allCombinedPoints
            .GroupBy(p => new { p.Date.Year, p.Date.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Take(ChartConstants.MaxRecentMonths)
            .ToDictionary(
                g => $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month)} {g.Key.Year}",
                g => $"{ChartConstants.DateRangeMonthPrefix}{g.Key.Year}-{g.Key.Month}");

        return new ChartDataResult(seriesData, availableYears, availableMonths);
    }

    public List<ChartDataPoint> ApplyDateFilter(List<ChartDataPoint> data, string dateRange)
    {
        if (dateRange == ChartConstants.DateRangeAll)
        {
            return data;
        }

        var today = DateTime.Today;

        if (dateRange == ChartConstants.DateRangeLastMonth)
        {
            return data.Where(d => d.Date >= today.AddDays(-30) && d.Date <= today).ToList();
        }

        if (dateRange == ChartConstants.DateRangeLastQuarter)
        {
            return data.Where(d => d.Date >= today.AddDays(-90) && d.Date <= today).ToList();
        }

        if (dateRange == ChartConstants.DateRangeLastYear)
        {
            return data.Where(d => d.Date >= today.AddDays(-365) && d.Date <= today).ToList();
        }

        if (dateRange.StartsWith(ChartConstants.DateRangeYearPrefix))
        {
            var year = int.Parse(dateRange.Replace(ChartConstants.DateRangeYearPrefix, ""));

            return data.Where(d => d.Date.Year == year).ToList();
        }

        if (dateRange.StartsWith(ChartConstants.DateRangeMonthPrefix))
        {
            var parts = dateRange.Replace(ChartConstants.DateRangeMonthPrefix, "").Split('-');
            var year = int.Parse(parts[0]);
            var month = int.Parse(parts[1]);

            return data.Where(d => d.Date.Year == year && d.Date.Month == month).ToList();
        }

        return data;
    }

    public (decimal? Min, decimal? Max) GetYAxisRange(IEnumerable<int> inputTypeIds)
    {
        var ids = inputTypeIds.Distinct().ToList();

        if (ids.Count != 1)
        {
            return (null, null); // Mixed types - use auto-scaling
        }

        var inputTypeId = ids.First();

        if (inputTypeId == InputTypeIds.StarRating || inputTypeId == InputTypeIds.Score)
        {
            return (0, 5);
        }

        if (inputTypeId == InputTypeIds.StarRating10 || inputTypeId == InputTypeIds.Score10)
        {
            return (0, 10);
        }

        if (inputTypeId == InputTypeIds.Percentage)
        {
            return (0, 100);
        }

        return (null, null); // Auto-scale for Integer/Decimal
    }

    public List<string> GenerateCorrelationInsights(
        List<ChartSeriesData> seriesData,
        Dictionary<int, string> inputTypeNames)
    {
        var insights = new List<string>();

        if (seriesData.Count < 2)
        {
            return insights;
        }

        // Check 1: Different input types (different scales)
        var inputTypes = seriesData.Select(s => s.InputTypeId).Distinct().ToList();
        if (inputTypes.Count > 1)
        {
            var typeNames = inputTypes
                .Where(id => inputTypeNames.ContainsKey(id))
                .Select(id => inputTypeNames[id])
                .ToList();

            if (typeNames.Count > 1)
            {
                insights.Add($"Tags use different scales ({string.Join(", ", typeNames)}) - values may not be directly comparable.");
            }
        }

        // Check 2: Date overlap analysis
        var dateRanges = seriesData
            .Where(s => s.DataPoints.Any())
            .Select(s => (
                Min: s.DataPoints.Min(d => d.Date),
                Max: s.DataPoints.Max(d => d.Date)))
            .ToList();

        if (dateRanges.Count >= 2)
        {
            var overallMin = dateRanges.Max(r => r.Min);
            var overallMax = dateRanges.Min(r => r.Max);

            if (overallMin > overallMax)
            {
                insights.Add("Date ranges don't overlap - tags were recorded in different time periods.");
            }
            else
            {
                var totalSpan = (dateRanges.Max(r => r.Max) - dateRanges.Min(r => r.Min)).TotalDays;
                var overlapSpan = (overallMax - overallMin).TotalDays;
                var overlapPercent = totalSpan > 0 ? (overlapSpan / totalSpan) * 100 : 0;

                if (overlapPercent < 50)
                {
                    insights.Add($"Limited date overlap ({overlapPercent:F0}%) - comparison may not reflect true correlation.");
                }
            }
        }

        // Check 3: Data density disparity
        var pointCounts = seriesData.Select(s => s.DataPoints.Count).ToList();
        var maxCount = pointCounts.Max();
        var minCount = pointCounts.Min();

        if (maxCount > minCount * 3 && minCount > 0)
        {
            var sparseTag = seriesData.First(s => s.DataPoints.Count == minCount).TagName;
            insights.Add($"'{sparseTag}' has significantly fewer data points - trends may be less reliable.");
        }

        // Check 4: Value range disparity (when same input type)
        if (inputTypes.Count == 1 && seriesData.All(s => s.DataPoints.Count >= 2))
        {
            var ranges = seriesData
                .Select(s => s.DataPoints.Max(d => d.Value) - s.DataPoints.Min(d => d.Value))
                .ToList();

            if (ranges.Min() > 0 && ranges.Max() > ranges.Min() * 5)
            {
                insights.Add("Value ranges vary significantly between tags - consider if comparison is meaningful.");
            }
        }

        return insights;
    }
}
