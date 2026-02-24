namespace LogMyDay.Domain.Constants;

/// <summary>
/// Constants for chart functionality including storage keys, colors, and configuration values.
/// </summary>
public static class ChartConstants
{
    // Chart types
    public const string LineChart = "line";
    public const string BarChart = "bar";

    // Date range options
    public const string DateRangeAll = "all";
    public const string DateRangeLastMonth = "last-month";
    public const string DateRangeLastQuarter = "last-quarter";
    public const string DateRangeLastYear = "last-year";
    public const string DateRangeYearPrefix = "year-";
    public const string DateRangeMonthPrefix = "month-";

    // LocalStorage keys for chart preferences
    public const string SelectedTagKey = "charts_selectedTag";
    public const string SelectedTag2Key = "charts_selectedTag2";
    public const string SelectedTag3Key = "charts_selectedTag3";
    public const string ChartType1Key = "charts_chartType1";
    public const string ChartType2Key = "charts_chartType2";
    public const string ChartType3Key = "charts_chartType3";
    public const string DateRangeKey = "charts_dateRange";

    // Configuration
    public const int MaxComparableTags = 3;
    public const int DefaultPageSize = 10000;
    public const int MaxRecentMonths = 12;
    public const string ChartHeight = "550px";

    // Series colors for multi-tag comparison
    public static readonly string[] SeriesColors = { "#0ea5e9", "#f59e0b", "#10b981" };

    // Theme colors
    public static class DarkTheme
    {
        public const string Background = "#1e293b";
        public const string ForeColor = "#e2e8f0";
        public const string GridBorder = "#334155";
        public const string TooltipBackground = "#1e293b";
        public const string TooltipText = "#f1f5f9";
        public const string TooltipDateText = "#94a3b8";
    }

    public static class LightTheme
    {
        public const string Background = "#ffffff";
        public const string ForeColor = "#374151";
        public const string GridBorder = "#e2e8f0";
        public const string TooltipBackground = "#ffffff";
        public const string TooltipText = "#1e293b";
        public const string TooltipDateText = "#64748b";
        public const string TooltipBorder = "#e2e8f0";
    }

    // Input type names for formatting decisions
    public const string DecimalTypeName = "Decimal";
    public const string DecimalPrecision2TypeName = "Decimal, precision 2";
}
