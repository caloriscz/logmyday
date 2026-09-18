using System.Globalization;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Services.Insights;

/// <summary>How a day's activity values are collapsed into the single value shown in a timeline cell.</summary>
public enum ComparisonAggregation
{
    First,
    Sum,
    Average,
    Count,
    Max
}

/// <summary>Whether a comparison row follows the reference row's dates or sits at a day offset from them.</summary>
public enum RowSyncMode
{
    Synchronized,
    Offset
}

/// <summary>
/// What a cell has to show. <see cref="Missing"/> and <see cref="NotApplicable"/> both render empty but
/// mean different things: no entry that day, versus entries that the chosen aggregation cannot express
/// as a number.
/// </summary>
public enum ComparisonCellState
{
    Missing,
    NotApplicable,
    Value
}

/// <summary>Unit by which the visible period moves.</summary>
public enum ComparisonStep
{
    Day,
    Week,
    Month,
    Year,
    Window
}

/// <summary>
/// The end of the visible period, held as an unmutated base date plus per-unit deltas rather than as a
/// single mutated date. This is what makes month and year navigation lossless: mutating in place turns
/// Jan 31 → +1 month → Feb 28 → −1 month into Jan 28, whereas re-resolving from the base returns Jan 31.
/// Resolution order is fixed at years, then months, then days.
/// </summary>
public readonly record struct AnchorPosition(DateTime BaseDate, int DayDelta, int MonthDelta, int YearDelta)
{
    public DateTime Resolve()
    {
        return BaseDate.Date
            .AddYears(YearDelta)
            .AddMonths(MonthDelta)
            .AddDays(DayDelta);
    }
}

/// <summary>Configuration of one timeline row: which tag, how it aligns to the reference row, how days collapse.</summary>
public sealed record ComparisonRowConfig(
    int? TagId,
    RowSyncMode SyncMode,
    int OffsetDays,
    ComparisonAggregation Aggregation)
{
    /// <summary>
    /// The offset actually applied. A synchronized row contributes no shift even when it still carries a
    /// previously configured <see cref="OffsetDays"/>, so toggling back to Offset restores the old value.
    /// </summary>
    public int EffectiveOffsetDays => SyncMode == RowSyncMode.Synchronized ? 0 : OffsetDays;

    /// <summary>The reference row — never offset.</summary>
    public static ComparisonRowConfig Anchor(int? tagId = null)
    {
        return new ComparisonRowConfig(tagId, RowSyncMode.Synchronized, 0, ComparisonAggregation.First);
    }

    public static ComparisonRowConfig Comparison(int? tagId = null)
    {
        return new ComparisonRowConfig(tagId, RowSyncMode.Synchronized, 0, ComparisonAggregation.First);
    }
}

/// <summary>The shared day columns every row is aligned to.</summary>
public sealed record ComparisonTimeline(
    AnchorPosition Anchor,
    int ColumnCount,
    IReadOnlyList<DateTime> Columns)
{
    public DateTime Start => Columns[0];

    public DateTime End => Columns[^1];
}

/// <summary>
/// One rendered cell. <see cref="ActualDate"/> is the row's own date, which differs from the column date
/// on offset rows and is what the cell tooltip surfaces.
/// </summary>
public sealed record ComparisonCell(
    DateTime ActualDate,
    ComparisonCellState State,
    IReadOnlyList<string> RawValues,
    decimal? NumericValue)
{
    /// <summary>
    /// Invariant rendering of the aggregated number, for <c>ColorSchemeIndex.ResolveColor</c>. Kept apart
    /// from display formatting on purpose: the resolver parses invariant, the user sees their own culture.
    /// </summary>
    public string? ColorKey => NumericValue?.ToString(CultureInfo.InvariantCulture);

    public static ComparisonCell Missing(DateTime actualDate)
    {
        return new ComparisonCell(actualDate, ComparisonCellState.Missing, [], null);
    }

    public static ComparisonCell NotApplicable(DateTime actualDate, IReadOnlyList<string> rawValues)
    {
        return new ComparisonCell(actualDate, ComparisonCellState.NotApplicable, rawValues, null);
    }

    public static ComparisonCell Numeric(DateTime actualDate, IReadOnlyList<string> rawValues, decimal value)
    {
        return new ComparisonCell(actualDate, ComparisonCellState.Value, rawValues, value);
    }

    /// <summary>A value that has no numeric reading — only <see cref="ComparisonAggregation.First"/> produces these.</summary>
    public static ComparisonCell Text(DateTime actualDate, IReadOnlyList<string> rawValues)
    {
        return new ComparisonCell(actualDate, ComparisonCellState.Value, rawValues, null);
    }
}

/// <summary>One assembled row: its config, the resolved tag, and one cell per timeline column.</summary>
public sealed record ComparisonRowResult(
    int Index,
    ComparisonRowConfig Config,
    TagResponse? Tag,
    IReadOnlyList<ComparisonCell> Cells);

/// <summary>Everything the page renders. Cells are index-aligned to <c>Timeline.Columns</c> across all rows.</summary>
public sealed record ComparisonView(
    ComparisonTimeline Timeline,
    IReadOnlyList<ComparisonRowResult> Rows);

/// <summary>Raw per-day values for every tag in the comparison, keyed by tag then by date.</summary>
public sealed record ComparisonDataSet(
    IReadOnlyDictionary<int, IReadOnlyDictionary<DateTime, IReadOnlyList<string>>> ValuesByTag,
    bool IsTruncated)
{
    public static ComparisonDataSet Empty { get; } =
        new(new Dictionary<int, IReadOnlyDictionary<DateTime, IReadOnlyList<string>>>(), false);

    public IReadOnlyList<string> GetValues(int tagId, DateTime date)
    {
        if (!ValuesByTag.TryGetValue(tagId, out var byDate))
        {
            return [];
        }

        return byDate.TryGetValue(date.Date, out var values) ? values : [];
    }
}

/// <summary>Persisted view configuration. The visible period is not stored — the page always opens at today.</summary>
public sealed record ComparisonPreferences(
    int ColumnCount,
    IReadOnlyList<ComparisonRowConfig> Rows)
{
    public static ComparisonPreferences Default { get; } = new(
        ComparisonConstants.DefaultColumnCount,
        [ComparisonRowConfig.Anchor(), ComparisonRowConfig.Comparison()]);
}
