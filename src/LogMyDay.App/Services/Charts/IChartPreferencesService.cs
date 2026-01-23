namespace LogMyDay.App.Services.Charts;

/// <summary>
/// Service for managing chart preferences in localStorage.
/// </summary>
public interface IChartPreferencesService
{
    /// <summary>
    /// Loads all chart preferences from storage.
    /// </summary>
    Task<ChartPreferences> LoadAsync(IEnumerable<int> validTagIds);

    /// <summary>
    /// Saves a tag selection to storage.
    /// </summary>
    Task SaveTagAsync(int slot, int? tagId);

    /// <summary>
    /// Saves a chart type preference to storage.
    /// </summary>
    Task SaveChartTypeAsync(int slot, string chartType);

    /// <summary>
    /// Saves the date range preference to storage.
    /// </summary>
    Task SaveDateRangeAsync(string dateRange);

    /// <summary>
    /// Detects if dark mode is currently enabled.
    /// </summary>
    Task<bool> IsDarkModeAsync();
}
