using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Services.Insights;

/// <summary>
/// Turns row configuration plus fetched day values into the rows the page renders. Pure, and the last
/// step before rendering: the page only reads the result, so offset arithmetic and aggregation never mix
/// into markup.
/// </summary>
public static class ComparisonViewBuilder
{
    /// <summary>
    /// Assembles every row against the shared columns. Cells stay index-aligned to
    /// <c>timeline.Columns</c> in every row — that alignment is what lets the page highlight a column as a
    /// single vertical band and what makes an offset row comparable to the reference row at a glance.
    /// </summary>
    public static ComparisonView Build(
        ComparisonTimeline timeline,
        IReadOnlyList<ComparisonRowConfig> rows,
        IReadOnlyDictionary<int, TagResponse> tagsById,
        ComparisonDataSet data)
    {
        var results = new List<ComparisonRowResult>(rows.Count);

        for (var index = 0; index < rows.Count; index++)
        {
            // The reference row defines the dates every other row is measured against, so it can never
            // carry an offset of its own — whatever was configured or restored from storage.
            var config = index == 0
                ? rows[index] with { SyncMode = RowSyncMode.Synchronized }
                : rows[index];

            var tag = config.TagId is int tagId && tagsById.TryGetValue(tagId, out var found) ? found : null;

            results.Add(new ComparisonRowResult(index, config, tag, BuildCells(timeline, config, data)));
        }

        return new ComparisonView(timeline, results);
    }

    private static IReadOnlyList<ComparisonCell> BuildCells(
        ComparisonTimeline timeline,
        ComparisonRowConfig config,
        ComparisonDataSet data)
    {
        var dates = ComparisonTimelineCalculator.BuildRowDates(timeline.Columns, config.EffectiveOffsetDays);
        var cells = new ComparisonCell[dates.Count];

        for (var i = 0; i < dates.Count; i++)
        {
            // An unconfigured row still renders a full width of empty cells, so adding a row does not
            // reflow the grid.
            var values = config.TagId is int tagId ? data.GetValues(tagId, dates[i]) : null;

            cells[i] = ComparisonAggregator.Aggregate(dates[i], values, config.Aggregation);
        }

        return cells;
    }
}
