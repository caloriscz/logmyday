namespace LogMyDay.App.Services.Insights;

/// <summary>
/// Limits and defaults for the Insights timeline comparison view. Deliberately App-local: the feature
/// is web-only, so nothing here belongs in <c>LogMyDay.Domain</c> where the mobile app would see it.
/// </summary>
public static class ComparisonConstants
{
    /// <summary>Selectable widths of the visible period, in day columns.</summary>
    public static readonly int[] ColumnCountOptions = { 7, 14, 30, 90 };

    public const int DefaultColumnCount = 14;

    /// <summary>A comparison needs at least a reference row and one row to compare it against.</summary>
    public const int MinRows = 2;

    public const int MaxRows = 5;

    /// <summary>Ten years — enough for year-over-year comparisons without allowing absurd ranges.</summary>
    public const int MaxOffsetDays = 3650;

    public const string StateStorageKey = "insights_compare_state";
}
