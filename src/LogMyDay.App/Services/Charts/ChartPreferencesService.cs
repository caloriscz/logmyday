using LogMyDay.Domain.Constants;
using Microsoft.JSInterop;

namespace LogMyDay.App.Services.Charts;

/// <summary>
/// Service for managing chart preferences in localStorage.
/// </summary>
public class ChartPreferencesService : IChartPreferencesService
{
    private readonly IJSRuntime _js;

    public ChartPreferencesService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<ChartPreferences> LoadAsync(IEnumerable<int> validTagIds)
    {
        var validIds = validTagIds.ToHashSet();

        try
        {
            var tag1 = await LoadTagIdAsync(ChartConstants.SelectedTagKey, validIds);
            var tag2 = await LoadTagIdAsync(ChartConstants.SelectedTag2Key, validIds);
            var tag3 = await LoadTagIdAsync(ChartConstants.SelectedTag3Key, validIds);

            var chartType1 = await LoadChartTypeAsync(ChartConstants.ChartType1Key);
            var chartType2 = await LoadChartTypeAsync(ChartConstants.ChartType2Key);
            var chartType3 = await LoadChartTypeAsync(ChartConstants.ChartType3Key);

            var dateRange = await LoadStringAsync(ChartConstants.DateRangeKey) ?? ChartConstants.DateRangeAll;

            return new ChartPreferences(tag1, tag2, tag3, chartType1, chartType2, chartType3, dateRange);
        }
        catch
        {
            return ChartPreferences.Default;
        }
    }

    public async Task SaveTagAsync(int slot, int? tagId)
    {
        var key = slot switch
        {
            1 => ChartConstants.SelectedTagKey,
            2 => ChartConstants.SelectedTag2Key,
            3 => ChartConstants.SelectedTag3Key,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };

        if (tagId.HasValue)
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, tagId.Value.ToString());
        }
        else
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", key);
        }
    }

    public async Task SaveChartTypeAsync(int slot, string chartType)
    {
        var key = slot switch
        {
            1 => ChartConstants.ChartType1Key,
            2 => ChartConstants.ChartType2Key,
            3 => ChartConstants.ChartType3Key,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };

        await _js.InvokeVoidAsync("localStorage.setItem", key, chartType);
    }

    public async Task SaveDateRangeAsync(string dateRange)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", ChartConstants.DateRangeKey, dateRange);
    }

    public async Task<bool> IsDarkModeAsync()
    {
        try
        {
            // Use a safer approach than eval - import a helper module
            return await _js.InvokeAsync<bool>("eval", "document.documentElement.classList.contains('dark')");
        }
        catch
        {
            return false;
        }
    }

    private async Task<int?> LoadTagIdAsync(string key, HashSet<int> validIds)
    {
        var value = await LoadStringAsync(key);
        if (!string.IsNullOrEmpty(value) && int.TryParse(value, out var id) && validIds.Contains(id))
        {
            return id;
        }

        return null;
    }

    private async Task<string> LoadChartTypeAsync(string key)
    {
        var value = await LoadStringAsync(key);
        if (!string.IsNullOrEmpty(value) && (value == ChartConstants.LineChart || value == ChartConstants.BarChart))
        {
            return value;
        }

        return ChartConstants.LineChart;
    }

    private async Task<string?> LoadStringAsync(string key)
    {
        try
        {
            return await _js.InvokeAsync<string>("localStorage.getItem", key);
        }
        catch
        {
            return null;
        }
    }
}
