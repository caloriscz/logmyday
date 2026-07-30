using LogMyDay.App.Services.Insights;

namespace LogMyDay.App.Tests;

public class ComparisonTimelineCalculatorTests
{
    private static AnchorPosition AnchorAt(int year, int month, int day)
    {
        return new AnchorPosition(new DateTime(year, month, day), 0, 0, 0);
    }

    // --- BuildColumns ---

    [Fact]
    public void BuildColumns_DefaultCount_ReturnsFourteenConsecutiveDaysEndingAtAnchor()
    {
        var columns = ComparisonTimelineCalculator.BuildColumns(
            new DateTime(2026, 3, 26),
            ComparisonConstants.DefaultColumnCount);

        Assert.Equal(14, columns.Count);
        Assert.Equal(new DateTime(2026, 3, 13), columns[0]);
        Assert.Equal(new DateTime(2026, 3, 26), columns[^1]);

        for (var i = 1; i < columns.Count; i++)
        {
            Assert.Equal(columns[i - 1].AddDays(1), columns[i]);
        }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    [InlineData(90)]
    public void BuildColumns_EachPreset_FirstColumnIsAnchorMinusCountMinusOne(int columnCount)
    {
        var anchorEnd = new DateTime(2026, 6, 15);

        var columns = ComparisonTimelineCalculator.BuildColumns(anchorEnd, columnCount);

        Assert.Equal(columnCount, columns.Count);
        Assert.Equal(anchorEnd.AddDays(-(columnCount - 1)), columns[0]);
        Assert.Equal(anchorEnd, columns[^1]);
    }

    [Fact]
    public void BuildColumns_AnchorWithTimeComponent_NormalizesToMidnight()
    {
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26, 23, 47, 11), 3);

        Assert.All(columns, c => Assert.Equal(TimeSpan.Zero, c.TimeOfDay));
        Assert.Equal(new DateTime(2026, 3, 26), columns[^1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildColumns_ZeroOrNegativeCount_Throws(int columnCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26), columnCount));
    }

    [Fact]
    public void BuildColumns_SpanningMonthBoundary_CrossesIntoPreviousMonth()
    {
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 3), 7);

        Assert.Equal(new DateTime(2026, 2, 25), columns[0]);
        Assert.Equal(new DateTime(2026, 3, 3), columns[^1]);
    }

    // --- ApplyOffset / BuildRowDates ---

    [Theory]
    [InlineData(0, 2026, 3, 26)]
    [InlineData(1, 2026, 3, 27)]
    [InlineData(-1, 2026, 3, 25)]
    [InlineData(7, 2026, 4, 2)]
    [InlineData(-365, 2025, 3, 26)]
    public void ApplyOffset_PositiveNegativeAndZero_ShiftsByCalendarDays(int offsetDays, int year, int month, int day)
    {
        var shifted = ComparisonTimelineCalculator.ApplyOffset(new DateTime(2026, 3, 26), offsetDays);

        Assert.Equal(new DateTime(year, month, day), shifted);
    }

    [Fact]
    public void ApplyOffset_DateWithTimeComponent_NormalizesToMidnight()
    {
        var shifted = ComparisonTimelineCalculator.ApplyOffset(new DateTime(2026, 3, 26, 18, 30, 0), 1);

        Assert.Equal(new DateTime(2026, 3, 27), shifted);
    }

    [Fact]
    public void BuildRowDates_SynchronizedRow_MatchesColumnsExactly()
    {
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26), 14);

        var dates = ComparisonTimelineCalculator.BuildRowDates(columns, 0);

        Assert.Equal(columns, dates);
    }

    [Fact]
    public void BuildRowDates_OffsetPlusOne_EachDateIsColumnPlusOneDay()
    {
        // The coffee-today vs sleep-the-following-night alignment.
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26), 14);

        var dates = ComparisonTimelineCalculator.BuildRowDates(columns, 1);

        Assert.Equal(columns.Count, dates.Count);

        for (var i = 0; i < columns.Count; i++)
        {
            Assert.Equal(columns[i].AddDays(1), dates[i]);
        }
    }

    [Fact]
    public void BuildRowDates_OffsetMinus365_EachDateIsColumnMinus365Days()
    {
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26), 30);

        var dates = ComparisonTimelineCalculator.BuildRowDates(columns, -365);

        for (var i = 0; i < columns.Count; i++)
        {
            Assert.Equal(columns[i].AddDays(-365), dates[i]);
        }
    }

    // --- Navigate: day, week, window ---

    [Fact]
    public void Navigate_Day_ForwardAndBackward_MovesOneDay()
    {
        var anchor = AnchorAt(2026, 3, 26);

        var forward = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Day, 1, 14);
        var backward = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Day, -1, 14);

        Assert.Equal(new DateTime(2026, 3, 27), forward.Resolve());
        Assert.Equal(new DateTime(2026, 3, 25), backward.Resolve());
    }

    [Fact]
    public void Navigate_Week_MovesSevenDays()
    {
        var anchor = AnchorAt(2026, 3, 26);

        var forward = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Week, 1, 14);
        var backward = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Week, -1, 14);

        Assert.Equal(new DateTime(2026, 4, 2), forward.Resolve());
        Assert.Equal(new DateTime(2026, 3, 19), backward.Resolve());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    [InlineData(90)]
    public void Navigate_Window_MovesByColumnCount(int columnCount)
    {
        var anchor = AnchorAt(2026, 6, 15);

        var moved = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Window, -1, columnCount);

        Assert.Equal(new DateTime(2026, 6, 15).AddDays(-columnCount), moved.Resolve());
    }

    [Fact]
    public void Navigate_RepeatedDaySteps_Accumulate()
    {
        var anchor = AnchorAt(2026, 3, 26);

        for (var i = 0; i < 5; i++)
        {
            anchor = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Day, -1, 14);
        }

        Assert.Equal(new DateTime(2026, 3, 21), anchor.Resolve());
    }

    // --- Navigate: month and year boundaries ---

    [Fact]
    public void Navigate_Month_FromJan31InLeapYear_ResolvesToFeb29()
    {
        var anchor = AnchorAt(2024, 1, 31);

        var moved = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Month, 1, 14);

        Assert.Equal(new DateTime(2024, 2, 29), moved.Resolve());
    }

    [Fact]
    public void Navigate_Month_FromJan31InNonLeapYear_ResolvesToFeb28()
    {
        var anchor = AnchorAt(2026, 1, 31);

        var moved = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Month, 1, 14);

        Assert.Equal(new DateTime(2026, 2, 28), moved.Resolve());
    }

    [Fact]
    public void Navigate_Month_ForwardThenBackward_ReturnsJan31()
    {
        // The reason AnchorPosition keeps deltas instead of mutating a date: mutating would land on Jan 28.
        var anchor = AnchorAt(2026, 1, 31);

        var forward = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Month, 1, 14);
        var back = ComparisonTimelineCalculator.Navigate(forward, ComparisonStep.Month, -1, 14);

        Assert.Equal(new DateTime(2026, 2, 28), forward.Resolve());
        Assert.Equal(new DateTime(2026, 1, 31), back.Resolve());
    }

    [Fact]
    public void Navigate_Month_AcrossYearBoundary_DecemberToJanuary()
    {
        var anchor = AnchorAt(2025, 12, 15);

        var forward = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Month, 1, 14);

        Assert.Equal(new DateTime(2026, 1, 15), forward.Resolve());
    }

    [Fact]
    public void Navigate_Month_BackwardAcrossYearBoundary_JanuaryToDecember()
    {
        var anchor = AnchorAt(2026, 1, 15);

        var backward = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Month, -1, 14);

        Assert.Equal(new DateTime(2025, 12, 15), backward.Resolve());
    }

    [Fact]
    public void Navigate_Year_FromLeapDay_ResolvesToFeb28()
    {
        var anchor = AnchorAt(2024, 2, 29);

        var moved = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Year, 1, 14);

        Assert.Equal(new DateTime(2025, 2, 28), moved.Resolve());
    }

    [Fact]
    public void Navigate_Year_ForwardThenBackward_ReturnsLeapDay()
    {
        var anchor = AnchorAt(2024, 2, 29);

        var forward = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Year, 1, 14);
        var back = ComparisonTimelineCalculator.Navigate(forward, ComparisonStep.Year, -1, 14);

        Assert.Equal(new DateTime(2024, 2, 29), back.Resolve());
    }

    [Fact]
    public void Resolve_AppliesYearsThenMonthsThenDays()
    {
        var anchor = new AnchorPosition(new DateTime(2026, 1, 31), 2, 1, 1);

        // 2026-01-31 -> +1y = 2027-01-31 -> +1mo = 2027-02-28 -> +2d = 2027-03-02
        Assert.Equal(new DateTime(2027, 3, 2), anchor.Resolve());
    }

    [Fact]
    public void Navigate_MixedSteps_KeepDeltasIndependent()
    {
        var anchor = AnchorAt(2026, 1, 31);

        anchor = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Month, 1, 14);
        anchor = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Day, 1, 14);
        anchor = ComparisonTimelineCalculator.Navigate(anchor, ComparisonStep.Day, -1, 14);

        Assert.Equal(1, anchor.MonthDelta);
        Assert.Equal(0, anchor.DayDelta);
        Assert.Equal(new DateTime(2026, 2, 28), anchor.Resolve());
    }

    // --- ResetToToday / CanNavigateForward ---

    [Fact]
    public void ResetToToday_ReturnsAnchorAtTodayWithZeroDeltas()
    {
        var today = new DateTime(2026, 7, 30);

        var anchor = ComparisonTimelineCalculator.ResetToToday(today);

        Assert.Equal(today, anchor.BaseDate);
        Assert.Equal(0, anchor.DayDelta);
        Assert.Equal(0, anchor.MonthDelta);
        Assert.Equal(0, anchor.YearDelta);
        Assert.Equal(today, anchor.Resolve());
    }

    [Fact]
    public void ResetToToday_TodayWithTime_NormalizesToDate()
    {
        var anchor = ComparisonTimelineCalculator.ResetToToday(new DateTime(2026, 7, 30, 14, 22, 9));

        Assert.Equal(new DateTime(2026, 7, 30), anchor.Resolve());
    }

    [Fact]
    public void CanNavigateForward_AnchorAtToday_ReturnsFalse()
    {
        var today = new DateTime(2026, 7, 30);

        Assert.False(ComparisonTimelineCalculator.CanNavigateForward(
            ComparisonTimelineCalculator.ResetToToday(today),
            today));
    }

    [Fact]
    public void CanNavigateForward_AnchorInPast_ReturnsTrue()
    {
        var today = new DateTime(2026, 7, 30);
        var anchor = ComparisonTimelineCalculator.Navigate(
            ComparisonTimelineCalculator.ResetToToday(today), ComparisonStep.Week, -1, 14);

        Assert.True(ComparisonTimelineCalculator.CanNavigateForward(anchor, today));
    }

    [Fact]
    public void CanNavigateForward_TodayWithTimeComponent_StillComparesByDate()
    {
        var today = new DateTime(2026, 7, 30, 23, 59, 0);
        var anchor = ComparisonTimelineCalculator.ResetToToday(today);

        Assert.False(ComparisonTimelineCalculator.CanNavigateForward(anchor, today));
    }

    // --- GetRequiredRange ---

    [Fact]
    public void GetRequiredRange_MixedOffsets_SpansMostNegativeToMostPositive()
    {
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26), 14);

        var range = ComparisonTimelineCalculator.GetRequiredRange(columns, [0, 1, -365]);

        Assert.Equal(columns[0].AddDays(-365), range.Start);
        Assert.Equal(columns[^1].AddDays(1), range.End);
    }

    [Fact]
    public void GetRequiredRange_AllSynchronized_EqualsColumnRange()
    {
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26), 14);

        var range = ComparisonTimelineCalculator.GetRequiredRange(columns, [0, 0]);

        Assert.Equal(columns[0], range.Start);
        Assert.Equal(columns[^1], range.End);
    }

    [Fact]
    public void GetRequiredRange_AllOffsetsNegative_StillIncludesAnchorColumns()
    {
        // The reference row is always at offset 0, so the window's own end must stay covered.
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26), 14);

        var range = ComparisonTimelineCalculator.GetRequiredRange(columns, [-7, -365]);

        Assert.Equal(columns[0].AddDays(-365), range.Start);
        Assert.Equal(columns[^1], range.End);
    }

    [Fact]
    public void GetRequiredRange_AllOffsetsPositive_StillIncludesAnchorColumns()
    {
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26), 14);

        var range = ComparisonTimelineCalculator.GetRequiredRange(columns, [1, 7]);

        Assert.Equal(columns[0], range.Start);
        Assert.Equal(columns[^1].AddDays(7), range.End);
    }

    [Fact]
    public void GetRequiredRange_NoOffsets_EqualsColumnRange()
    {
        var columns = ComparisonTimelineCalculator.BuildColumns(new DateTime(2026, 3, 26), 7);

        var range = ComparisonTimelineCalculator.GetRequiredRange(columns, []);

        Assert.Equal(columns[0], range.Start);
        Assert.Equal(columns[^1], range.End);
    }

    // --- BuildTimeline ---

    [Fact]
    public void BuildTimeline_ExposesResolvedStartAndEnd()
    {
        var timeline = ComparisonTimelineCalculator.BuildTimeline(AnchorAt(2026, 3, 26), 14);

        Assert.Equal(14, timeline.ColumnCount);
        Assert.Equal(new DateTime(2026, 3, 13), timeline.Start);
        Assert.Equal(new DateTime(2026, 3, 26), timeline.End);
    }

    [Fact]
    public void BuildTimeline_AfterMonthNavigation_WindowMovesButWidthHolds()
    {
        var anchor = ComparisonTimelineCalculator.Navigate(AnchorAt(2026, 3, 26), ComparisonStep.Month, -1, 14);

        var timeline = ComparisonTimelineCalculator.BuildTimeline(anchor, 14);

        Assert.Equal(new DateTime(2026, 2, 26), timeline.End);
        Assert.Equal(14, timeline.Columns.Count);
    }
}
