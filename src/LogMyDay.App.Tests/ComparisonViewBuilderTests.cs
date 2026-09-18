using LogMyDay.App.Services.Insights;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Tests;

public class ComparisonViewBuilderTests
{
    private const int CoffeeTagId = 1;
    private const int SleepTagId = 2;
    private const int AlcoholTagId = 3;

    private static readonly DateTime AnchorEnd = new(2026, 3, 26);

    private static ComparisonTimeline Timeline(int columnCount = 14)
    {
        return ComparisonTimelineCalculator.BuildTimeline(new AnchorPosition(AnchorEnd, 0, 0, 0), columnCount);
    }

    private static IReadOnlyDictionary<int, TagResponse> Tags()
    {
        return new Dictionary<int, TagResponse>
        {
            [CoffeeTagId] = new() { Id = CoffeeTagId, Title = "Coffee", TypeId = 1, InputTypeId = 1 },
            [SleepTagId] = new() { Id = SleepTagId, Title = "Sleep", TypeId = 6, InputTypeId = 6 },
            [AlcoholTagId] = new() { Id = AlcoholTagId, Title = "Alcohol", TypeId = 3, InputTypeId = 3 }
        };
    }

    private static ComparisonDataSet DataSet(params (int TagId, DateTime Date, string[] Values)[] entries)
    {
        var byTag = new Dictionary<int, IReadOnlyDictionary<DateTime, IReadOnlyList<string>>>();

        foreach (var group in entries.GroupBy(e => e.TagId))
        {
            byTag[group.Key] = group.ToDictionary(
                e => e.Date.Date,
                e => (IReadOnlyList<string>)e.Values);
        }

        return new ComparisonDataSet(byTag, false);
    }

    // --- Yes/No rows ---

    [Theory]
    [InlineData(ComparisonAggregation.Sum)]
    [InlineData(ComparisonAggregation.Count)]
    [InlineData(ComparisonAggregation.Average)]
    [InlineData(ComparisonAggregation.Max)]
    public void Build_YesNoRow_AlwaysUsesFirst(ComparisonAggregation stored)
    {
        // A mode saved before Yes/No rows lost their aggregation choice must not leave the row
        // uncoloured or showing a meaningless "Sum" of 0/1.
        var rows = new[] { new ComparisonRowConfig(AlcoholTagId, RowSyncMode.Synchronized, 0, stored) };

        var view = ComparisonViewBuilder.Build(Timeline(), rows, Tags(), DataSet((AlcoholTagId, AnchorEnd, new[] { "true" })));

        Assert.Equal(ComparisonAggregation.First, view.Rows[0].Config.Aggregation);
        Assert.Equal(1, view.Rows[0].Cells[^1].NumericValue);
    }

    [Fact]
    public void Build_NumericRow_KeepsStoredAggregation()
    {
        var rows = new[] { new ComparisonRowConfig(CoffeeTagId, RowSyncMode.Synchronized, 0, ComparisonAggregation.Sum) };

        var view = ComparisonViewBuilder.Build(Timeline(), rows, Tags(), DataSet());

        Assert.Equal(ComparisonAggregation.Sum, view.Rows[0].Config.Aggregation);
    }

    // --- Alignment ---

    [Fact]
    public void Build_TwoSynchronizedRows_AllRowsShareColumnDates()
    {
        var timeline = Timeline();

        var view = ComparisonViewBuilder.Build(
            timeline,
            [ComparisonRowConfig.Anchor(CoffeeTagId), ComparisonRowConfig.Comparison(SleepTagId)],
            Tags(),
            DataSet());

        foreach (var row in view.Rows)
        {
            Assert.Equal(timeline.Columns, row.Cells.Select(c => c.ActualDate).ToList());
        }
    }

    [Fact]
    public void Build_ColumnIndexAlignment_RowCellsAreIndexAlignedToColumns()
    {
        // The invariant the column highlight and the sticky-column layout both depend on.
        var timeline = Timeline(30);

        var view = ComparisonViewBuilder.Build(
            timeline,
            [
                ComparisonRowConfig.Anchor(CoffeeTagId),
                ComparisonRowConfig.Comparison(SleepTagId) with { SyncMode = RowSyncMode.Offset, OffsetDays = 1 },
                ComparisonRowConfig.Comparison(CoffeeTagId) with { SyncMode = RowSyncMode.Offset, OffsetDays = -365 }
            ],
            Tags(),
            DataSet());

        Assert.All(view.Rows, row => Assert.Equal(timeline.Columns.Count, row.Cells.Count));

        for (var i = 0; i < timeline.Columns.Count; i++)
        {
            Assert.Equal(timeline.Columns[i], view.Rows[0].Cells[i].ActualDate);
            Assert.Equal(timeline.Columns[i].AddDays(1), view.Rows[1].Cells[i].ActualDate);
            Assert.Equal(timeline.Columns[i].AddDays(-365), view.Rows[2].Cells[i].ActualDate);
        }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    [InlineData(90)]
    public void Build_EachPreset_CellCountPerRowEqualsColumnCount(int columnCount)
    {
        var timeline = Timeline(columnCount);

        var view = ComparisonViewBuilder.Build(
            timeline,
            [ComparisonRowConfig.Anchor(CoffeeTagId), ComparisonRowConfig.Comparison(SleepTagId)],
            Tags(),
            DataSet());

        Assert.All(view.Rows, row => Assert.Equal(columnCount, row.Cells.Count));
    }

    // --- Offsets ---

    [Fact]
    public void Build_OffsetPlusOne_SecondRowCellsAreOneDayAhead()
    {
        // Coffee on the 25th against sleep on the night of the 26th: both land in the same column.
        var timeline = Timeline(3);
        var coffeeDay = timeline.Columns[0];

        var view = ComparisonViewBuilder.Build(
            timeline,
            [
                ComparisonRowConfig.Anchor(CoffeeTagId),
                ComparisonRowConfig.Comparison(SleepTagId) with { SyncMode = RowSyncMode.Offset, OffsetDays = 1 }
            ],
            Tags(),
            DataSet(
                (CoffeeTagId, coffeeDay, ["4"]),
                (SleepTagId, coffeeDay.AddDays(1), ["5.5"])));

        Assert.Equal(4m, view.Rows[0].Cells[0].NumericValue);
        Assert.Equal(5.5m, view.Rows[1].Cells[0].NumericValue);
        Assert.Equal(coffeeDay.AddDays(1), view.Rows[1].Cells[0].ActualDate);
    }

    [Fact]
    public void Build_OffsetMinus365_SameTagInBothRows_YieldsDifferentValues()
    {
        var timeline = Timeline(3);
        var thisYear = timeline.Columns[0];

        var view = ComparisonViewBuilder.Build(
            timeline,
            [
                ComparisonRowConfig.Anchor(CoffeeTagId),
                ComparisonRowConfig.Comparison(CoffeeTagId) with { SyncMode = RowSyncMode.Offset, OffsetDays = -365 }
            ],
            Tags(),
            DataSet(
                (CoffeeTagId, thisYear, ["3"]),
                (CoffeeTagId, thisYear.AddDays(-365), ["6"])));

        Assert.Equal(3m, view.Rows[0].Cells[0].NumericValue);
        Assert.Equal(6m, view.Rows[1].Cells[0].NumericValue);
    }

    [Fact]
    public void Build_SynchronizedRowWithStoredOffset_IgnoresOffset()
    {
        // Toggling back to Sync must not lose the stored offset, but must not apply it either.
        var timeline = Timeline(3);

        var view = ComparisonViewBuilder.Build(
            timeline,
            [
                ComparisonRowConfig.Anchor(CoffeeTagId),
                ComparisonRowConfig.Comparison(SleepTagId) with { SyncMode = RowSyncMode.Synchronized, OffsetDays = 7 }
            ],
            Tags(),
            DataSet());

        Assert.Equal(timeline.Columns[0], view.Rows[1].Cells[0].ActualDate);
        Assert.Equal(7, view.Rows[1].Config.OffsetDays);
    }

    [Fact]
    public void Build_AnchorRowConfiguredAsOffset_IsForcedSynchronized()
    {
        var timeline = Timeline(3);

        var view = ComparisonViewBuilder.Build(
            timeline,
            [
                ComparisonRowConfig.Anchor(CoffeeTagId) with { SyncMode = RowSyncMode.Offset, OffsetDays = 5 },
                ComparisonRowConfig.Comparison(SleepTagId)
            ],
            Tags(),
            DataSet());

        Assert.Equal(RowSyncMode.Synchronized, view.Rows[0].Config.SyncMode);
        Assert.Equal(timeline.Columns[0], view.Rows[0].Cells[0].ActualDate);
    }

    // --- Tags and missing data ---

    [Fact]
    public void Build_RowWithoutTag_ProducesAllMissingCells()
    {
        var timeline = Timeline(7);

        var view = ComparisonViewBuilder.Build(
            timeline,
            [ComparisonRowConfig.Anchor(CoffeeTagId), ComparisonRowConfig.Comparison()],
            Tags(),
            DataSet((CoffeeTagId, timeline.Columns[0], ["1"])));

        Assert.Null(view.Rows[1].Tag);
        Assert.Equal(7, view.Rows[1].Cells.Count);
        Assert.All(view.Rows[1].Cells, c => Assert.Equal(ComparisonCellState.Missing, c.State));
    }

    [Fact]
    public void Build_UnknownTagId_ResolvesTagToNull()
    {
        var timeline = Timeline(3);

        var view = ComparisonViewBuilder.Build(
            timeline,
            [ComparisonRowConfig.Anchor(999), ComparisonRowConfig.Comparison(SleepTagId)],
            Tags(),
            DataSet());

        Assert.Null(view.Rows[0].Tag);
    }

    [Fact]
    public void Build_ResolvesTagsFromLookup()
    {
        var view = ComparisonViewBuilder.Build(
            Timeline(3),
            [ComparisonRowConfig.Anchor(CoffeeTagId), ComparisonRowConfig.Comparison(SleepTagId)],
            Tags(),
            DataSet());

        Assert.Equal("Coffee", view.Rows[0].Tag?.Title);
        Assert.Equal("Sleep", view.Rows[1].Tag?.Title);
    }

    [Fact]
    public void Build_DayWithNoData_ProducesMissingCell()
    {
        var timeline = Timeline(3);

        var view = ComparisonViewBuilder.Build(
            timeline,
            [ComparisonRowConfig.Anchor(CoffeeTagId), ComparisonRowConfig.Comparison(SleepTagId)],
            Tags(),
            DataSet((CoffeeTagId, timeline.Columns[1], ["2"])));

        Assert.Equal(ComparisonCellState.Missing, view.Rows[0].Cells[0].State);
        Assert.Equal(ComparisonCellState.Value, view.Rows[0].Cells[1].State);
        Assert.Equal(ComparisonCellState.Missing, view.Rows[0].Cells[2].State);
    }

    // --- Aggregation is per row ---

    [Fact]
    public void Build_EachRowUsesItsOwnAggregation()
    {
        var timeline = Timeline(1);
        var day = timeline.Columns[0];
        var data = DataSet((CoffeeTagId, day, ["2", "4"]));

        var view = ComparisonViewBuilder.Build(
            timeline,
            [
                ComparisonRowConfig.Anchor(CoffeeTagId) with { Aggregation = ComparisonAggregation.Average },
                ComparisonRowConfig.Comparison(CoffeeTagId) with { Aggregation = ComparisonAggregation.Count }
            ],
            Tags(),
            data);

        Assert.Equal(3m, view.Rows[0].Cells[0].NumericValue);
        Assert.Equal(2m, view.Rows[1].Cells[0].NumericValue);
    }

    [Fact]
    public void Build_PreservesRowIndexAndConfig()
    {
        var view = ComparisonViewBuilder.Build(
            Timeline(3),
            [
                ComparisonRowConfig.Anchor(CoffeeTagId),
                ComparisonRowConfig.Comparison(SleepTagId) with { Aggregation = ComparisonAggregation.Max },
                ComparisonRowConfig.Comparison()
            ],
            Tags(),
            DataSet());

        Assert.Equal([0, 1, 2], view.Rows.Select(r => r.Index).ToList());
        Assert.Equal(ComparisonAggregation.Max, view.Rows[1].Config.Aggregation);
    }

    [Fact]
    public void Build_ExposesTheTimelineItWasGiven()
    {
        var timeline = Timeline(30);

        var view = ComparisonViewBuilder.Build(
            timeline,
            [ComparisonRowConfig.Anchor(CoffeeTagId), ComparisonRowConfig.Comparison(SleepTagId)],
            Tags(),
            DataSet());

        Assert.Same(timeline, view.Timeline);
    }
}
