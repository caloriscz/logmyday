namespace LogMyDay.Shared.Preferences;

public static class ActivityFilterPreferences
{
    public const string DailyDisplayType = "daily";
    public const string WeeklyDisplayType = "weekly";
    public const string MonthlyDisplayType = "monthly";
    public const string DescSortOrder = "desc";
    public const string AscSortOrder = "asc";
    public const string GroupAscSortOrder = "group-asc";
    public const string GroupDescSortOrder = "group-desc";

    public static string NormalizeDisplayType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            WeeklyDisplayType => WeeklyDisplayType,
            MonthlyDisplayType => MonthlyDisplayType,
            _ => DailyDisplayType,
        };
    }

    public static string NormalizeActivitySortOrder(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            AscSortOrder => AscSortOrder,
            GroupAscSortOrder => GroupAscSortOrder,
            GroupDescSortOrder => GroupDescSortOrder,
            _ => DescSortOrder,
        };
    }

    public static string NormalizePeriodSort(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            AscSortOrder => AscSortOrder,
            _ => DescSortOrder,
        };
    }
}