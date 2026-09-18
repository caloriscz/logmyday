using System.Text.Json;
using Microsoft.JSInterop;

namespace LogMyDay.App.Services.Insights;

/// <summary>
/// Persists the comparison setup in localStorage as a single JSON blob, following
/// <c>ChartPreferencesService</c>. The visible period is deliberately not stored — the page always opens
/// at today, so a bookmarked view never shows a stale window.
/// </summary>
public class ComparisonPreferencesService : IComparisonPreferencesService
{
    private readonly IJSRuntime _js;

    public ComparisonPreferencesService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>What actually goes into storage. Kept separate from the domain record so a future field
    /// rename cannot silently invalidate everyone's saved setup.</summary>
    private sealed record StoredState(int ColumnCount, List<StoredRow> Rows);

    private sealed record StoredRow(int? TagId, RowSyncMode SyncMode, int OffsetDays, ComparisonAggregation Aggregation);

    public async Task<ComparisonPreferences> Load(IEnumerable<int> validTagIds)
    {
        var json = await ReadItem(ComparisonConstants.StateStorageKey);

        if (string.IsNullOrWhiteSpace(json))
        {
            return ComparisonPreferences.Default;
        }

        StoredState? stored;

        try
        {
            stored = JsonSerializer.Deserialize<StoredState>(json);
        }
        catch (JsonException)
        {
            // A blob written by an older or hand-edited version must not break the page.
            return ComparisonPreferences.Default;
        }

        if (stored is null || stored.Rows is null || stored.Rows.Count == 0)
        {
            return ComparisonPreferences.Default;
        }

        var validIds = validTagIds.ToHashSet();
        var columnCount = ComparisonConstants.ColumnCountOptions.Contains(stored.ColumnCount)
            ? stored.ColumnCount
            : ComparisonConstants.DefaultColumnCount;

        var rows = stored.Rows
            .Take(ComparisonConstants.MaxRows)
            .Select((row, index) => Restore(row, index, validIds))
            .ToList();

        while (rows.Count < ComparisonConstants.MinRows)
        {
            rows.Add(ComparisonRowConfig.Comparison());
        }

        return new ComparisonPreferences(columnCount, rows);
    }

    public async Task Save(ComparisonPreferences preferences)
    {
        var state = new StoredState(
            preferences.ColumnCount,
            preferences.Rows
                .Select(r => new StoredRow(r.TagId, r.SyncMode, r.OffsetDays, r.Aggregation))
                .ToList());

        await WriteItem(ComparisonConstants.StateStorageKey, JsonSerializer.Serialize(state));
    }

    private static ComparisonRowConfig Restore(StoredRow row, int index, HashSet<int> validIds)
    {
        // A tag that has since been deleted or renamed away must not leave the row pointing at nothing.
        var tagId = row.TagId is int id && validIds.Contains(id) ? id : (int?)null;

        // The reference row defines the dates, so it can never carry an offset.
        var syncMode = index == 0 ? RowSyncMode.Synchronized : row.SyncMode;

        var offsetDays = Math.Clamp(
            row.OffsetDays,
            -ComparisonConstants.MaxOffsetDays,
            ComparisonConstants.MaxOffsetDays);

        var aggregation = Enum.IsDefined(row.Aggregation) ? row.Aggregation : ComparisonAggregation.First;

        return new ComparisonRowConfig(tagId, syncMode, index == 0 ? 0 : offsetDays, aggregation);
    }

    private async Task<string?> ReadItem(string key)
    {
        try
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch
        {
            // localStorage genuinely is not reachable during prerendering; the same guard exists in
            // ChartPreferencesService and Calendar.OnAfterRenderAsync.
            return null;
        }
    }

    private async Task WriteItem(string key, string value)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch
        {
            // Nothing to recover: losing a preference write is not worth failing a render over.
        }
    }
}
