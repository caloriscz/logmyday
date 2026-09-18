namespace LogMyDay.App.Services.Insights;

/// <summary>
/// All date arithmetic behind the timeline comparison view: the visible day columns, per-row offsets,
/// period navigation, and the union range that has to be fetched. Entirely pure — "today" is always a
/// parameter, never read from the clock — so the view's behaviour at month, year and leap-day boundaries
/// is testable without rendering anything.
/// </summary>
public static class ComparisonTimelineCalculator
{
    /// <summary>
    /// The visible day columns, right-anchored: <paramref name="anchorEnd"/> is the last column and the
    /// window extends backwards. Anchoring at the end keeps "today" in the rightmost column on load,
    /// which is how the period reads naturally.
    /// </summary>
    public static IReadOnlyList<DateTime> BuildColumns(DateTime anchorEnd, int columnCount)
    {
        if (columnCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount), columnCount, "A timeline needs at least one column.");
        }

        var start = anchorEnd.Date.AddDays(-(columnCount - 1));
        var columns = new DateTime[columnCount];

        for (var i = 0; i < columnCount; i++)
        {
            columns[i] = start.AddDays(i);
        }

        return columns;
    }

    public static ComparisonTimeline BuildTimeline(AnchorPosition anchor, int columnCount)
    {
        return new ComparisonTimeline(anchor, columnCount, BuildColumns(anchor.Resolve(), columnCount));
    }

    /// <summary>Calendar-day shift. Dates are date-only, so this is DST-safe.</summary>
    public static DateTime ApplyOffset(DateTime date, int offsetDays)
    {
        return date.Date.AddDays(offsetDays);
    }

    /// <summary>
    /// The dates one row actually reads, given the shared columns and that row's offset. This is the
    /// row-alignment primitive: the result stays index-aligned to <paramref name="columns"/>, which is what
    /// lets an offset row sit under the reference row and still line up column for column.
    /// </summary>
    public static IReadOnlyList<DateTime> BuildRowDates(IReadOnlyList<DateTime> columns, int offsetDays)
    {
        var dates = new DateTime[columns.Count];

        for (var i = 0; i < columns.Count; i++)
        {
            dates[i] = ApplyOffset(columns[i], offsetDays);
        }

        return dates;
    }

    /// <summary>
    /// Moves the visible period by one step. Accumulates into the matching delta and never resolves or
    /// clamps — that happens only in <see cref="AnchorPosition.Resolve"/>, which is what makes month and
    /// year navigation reversible (Jan 31 forward then back is Jan 31, not Jan 28).
    /// </summary>
    /// <param name="direction">-1 for earlier, +1 for later. Any negative value counts as earlier.</param>
    public static AnchorPosition Navigate(AnchorPosition anchor, ComparisonStep step, int direction, int columnCount)
    {
        var sign = direction < 0 ? -1 : 1;

        return step switch
        {
            ComparisonStep.Day => anchor with { DayDelta = anchor.DayDelta + sign },
            ComparisonStep.Week => anchor with { DayDelta = anchor.DayDelta + (sign * 7) },
            ComparisonStep.Window => anchor with { DayDelta = anchor.DayDelta + (sign * columnCount) },
            ComparisonStep.Month => anchor with { MonthDelta = anchor.MonthDelta + sign },
            ComparisonStep.Year => anchor with { YearDelta = anchor.YearDelta + sign },
            _ => anchor
        };
    }

    public static AnchorPosition ResetToToday(DateTime todayInDisplayZone)
    {
        return new AnchorPosition(todayInDisplayZone.Date, 0, 0, 0);
    }

    /// <summary>
    /// Whether the period can still move forward. Mirrors Calendar's "Newer" bound: there is no data in
    /// the future, so the window never advances past today.
    /// </summary>
    public static bool CanNavigateForward(AnchorPosition anchor, DateTime today)
    {
        return anchor.Resolve() < today.Date;
    }

    /// <summary>
    /// The single date range covering every row, so one fetch per tag serves the whole view. Folds 0 into
    /// the offset set so the reference row's own columns stay covered even when every configured row is
    /// offset in the same direction.
    /// </summary>
    public static (DateTime Start, DateTime End) GetRequiredRange(
        IReadOnlyList<DateTime> columns,
        IEnumerable<int> effectiveOffsets)
    {
        var offsets = effectiveOffsets.Append(0).ToList();

        return (
            columns[0].AddDays(offsets.Min()),
            columns[^1].AddDays(offsets.Max()));
    }
}
