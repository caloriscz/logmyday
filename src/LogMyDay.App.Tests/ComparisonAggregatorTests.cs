using System.Globalization;
using LogMyDay.App.Services.Insights;

namespace LogMyDay.App.Tests;

public class ComparisonAggregatorTests
{
    private static readonly DateTime Day = new(2026, 3, 26);

    // --- Missing days ---

    [Theory]
    [InlineData(ComparisonAggregation.First)]
    [InlineData(ComparisonAggregation.Sum)]
    [InlineData(ComparisonAggregation.Average)]
    [InlineData(ComparisonAggregation.Count)]
    [InlineData(ComparisonAggregation.Max)]
    public void Aggregate_NullValues_ReturnsMissingCell(ComparisonAggregation mode)
    {
        var cell = ComparisonAggregator.Aggregate(Day, null, mode);

        Assert.Equal(ComparisonCellState.Missing, cell.State);
        Assert.Null(cell.NumericValue);
        Assert.Empty(cell.RawValues);
        Assert.Equal(Day, cell.ActualDate);
    }

    [Theory]
    [InlineData(ComparisonAggregation.First)]
    [InlineData(ComparisonAggregation.Sum)]
    [InlineData(ComparisonAggregation.Average)]
    [InlineData(ComparisonAggregation.Count)]
    [InlineData(ComparisonAggregation.Max)]
    public void Aggregate_EmptyList_ReturnsMissingCell(ComparisonAggregation mode)
    {
        var cell = ComparisonAggregator.Aggregate(Day, [], mode);

        Assert.Equal(ComparisonCellState.Missing, cell.State);
    }

    [Fact]
    public void Aggregate_Count_MissingDay_IsMissingNotZero()
    {
        // "Nothing logged" must stay visually distinct from "logged a zero", as in Calendar.
        var cell = ComparisonAggregator.Aggregate(Day, [], ComparisonAggregation.Count);

        Assert.Equal(ComparisonCellState.Missing, cell.State);
        Assert.Null(cell.NumericValue);
    }

    // --- First ---

    [Fact]
    public void Aggregate_First_ReturnsFirstRawValueVerbatim()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["7.5", "8"], ComparisonAggregation.First);

        Assert.Equal(ComparisonCellState.Value, cell.State);
        Assert.Equal(7.5m, cell.NumericValue);
        Assert.Equal("7.5", ComparisonAggregator.Format(cell, ComparisonAggregation.First, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Aggregate_First_NonNumericValue_StillReturnsValueState()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["felt rough"], ComparisonAggregation.First);

        Assert.Equal(ComparisonCellState.Value, cell.State);
        Assert.Null(cell.NumericValue);
        Assert.Null(cell.ColorKey);
        Assert.Equal("felt rough", ComparisonAggregator.Format(cell, ComparisonAggregation.First, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Aggregate_First_NonNumericFirstThenNumeric_UsesTheFirst()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["abc", "7"], ComparisonAggregation.First);

        Assert.Null(cell.NumericValue);
        Assert.Equal("abc", ComparisonAggregator.Format(cell, ComparisonAggregation.First, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Aggregate_First_NumericValue_SetsNumericValue()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["4"], ComparisonAggregation.First);

        Assert.Equal(4m, cell.NumericValue);
        Assert.Equal("4", cell.ColorKey);
    }

    // --- Sum ---

    [Fact]
    public void Aggregate_Sum_NumericValues_ReturnsSum()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["1", "2", "3.5"], ComparisonAggregation.Sum);

        Assert.Equal(ComparisonCellState.Value, cell.State);
        Assert.Equal(6.5m, cell.NumericValue);
    }

    [Fact]
    public void Aggregate_Sum_MixedValues_IgnoresNonNumeric()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["2", "not a number", "3"], ComparisonAggregation.Sum);

        Assert.Equal(5m, cell.NumericValue);
        Assert.Equal(3, cell.RawValues.Count);
    }

    [Fact]
    public void Aggregate_Sum_AllNonNumeric_ReturnsNotApplicable()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["abc", "def"], ComparisonAggregation.Sum);

        Assert.Equal(ComparisonCellState.NotApplicable, cell.State);
        Assert.Null(cell.NumericValue);
    }

    [Fact]
    public void Aggregate_Sum_SingleValue_ReturnsThatValue()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["3"], ComparisonAggregation.Sum);

        Assert.Equal(3m, cell.NumericValue);
    }

    // --- Average ---

    [Fact]
    public void Aggregate_Average_MixedValues_AveragesOnlyNumericValues()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["2", "abc", "4"], ComparisonAggregation.Average);

        Assert.Equal(3m, cell.NumericValue);
    }

    [Fact]
    public void Aggregate_Average_AllNonNumeric_ReturnsNotApplicable()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["abc"], ComparisonAggregation.Average);

        Assert.Equal(ComparisonCellState.NotApplicable, cell.State);
    }

    // --- Count ---

    [Fact]
    public void Aggregate_Count_CountsAllEntriesIncludingNonNumeric()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["1", "abc", "3"], ComparisonAggregation.Count);

        Assert.Equal(ComparisonCellState.Value, cell.State);
        Assert.Equal(3m, cell.NumericValue);
    }

    [Fact]
    public void Aggregate_Count_SingleEntry_ReturnsOne()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["anything"], ComparisonAggregation.Count);

        Assert.Equal(1m, cell.NumericValue);
    }

    // --- Max ---

    [Fact]
    public void Aggregate_Max_MultipleValues_ReturnsLargest()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["2", "9.5", "4"], ComparisonAggregation.Max);

        Assert.Equal(9.5m, cell.NumericValue);
    }

    [Fact]
    public void Aggregate_Max_NegativeValues_ReturnsLargest()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["-5", "-2", "-9"], ComparisonAggregation.Max);

        Assert.Equal(-2m, cell.NumericValue);
    }

    [Fact]
    public void Aggregate_Max_AllNonNumeric_ReturnsNotApplicable()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["abc", "def"], ComparisonAggregation.Max);

        Assert.Equal(ComparisonCellState.NotApplicable, cell.State);
    }

    // --- Parsing is invariant, matching ChartDataService ---

    [Fact]
    public void Aggregate_DecimalWithPointSeparator_ParsesAsNumeric()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["7.5"], ComparisonAggregation.Sum);

        Assert.Equal(7.5m, cell.NumericValue);
    }

    [Fact]
    public void Aggregate_DecimalWithCommaSeparator_TreatedAsNonNumeric()
    {
        // Pins invariant parsing: NumberStyles.Float rejects the comma rather than reading it as a group
        // separator, so "7,5" never silently becomes 75.
        var cell = ComparisonAggregator.Aggregate(Day, ["7,5"], ComparisonAggregation.Sum);

        Assert.Equal(ComparisonCellState.NotApplicable, cell.State);
    }

    [Fact]
    public void ColorKey_UnderCommaDecimalCulture_UsesInvariantFormat()
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("cs-CZ");
            var cell = ComparisonAggregator.Aggregate(Day, ["7.5"], ComparisonAggregation.Sum);

            Assert.Equal("7.5", cell.ColorKey);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Format_UnderCommaDecimalCulture_UsesThatCulture()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["7.5"], ComparisonAggregation.Sum);

        var formatted = ComparisonAggregator.Format(cell, ComparisonAggregation.Sum, new CultureInfo("cs-CZ"));

        Assert.Equal("7,5", formatted);
    }

    // --- Format ---

    [Fact]
    public void Format_Average_RoundsToTwoDecimals()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["1", "2"], ComparisonAggregation.Average);

        var formatted = ComparisonAggregator.Format(cell, ComparisonAggregation.Average, CultureInfo.InvariantCulture);

        Assert.Equal("1.5", formatted);
    }

    [Fact]
    public void Format_Average_RepeatingDecimal_RoundsToTwoPlaces()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["1", "1", "2"], ComparisonAggregation.Average);

        var formatted = ComparisonAggregator.Format(cell, ComparisonAggregation.Average, CultureInfo.InvariantCulture);

        Assert.Equal("1.33", formatted);
    }

    [Fact]
    public void Format_Count_RendersAsInteger()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["a", "b", "c"], ComparisonAggregation.Count);

        Assert.Equal("3", ComparisonAggregator.Format(cell, ComparisonAggregation.Count, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_MissingCell_ReturnsNull()
    {
        var cell = ComparisonAggregator.Aggregate(Day, [], ComparisonAggregation.Sum);

        Assert.Null(ComparisonAggregator.Format(cell, ComparisonAggregation.Sum, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_NotApplicableCell_ReturnsNull()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["abc"], ComparisonAggregation.Sum);

        Assert.Null(ComparisonAggregator.Format(cell, ComparisonAggregation.Sum, CultureInfo.InvariantCulture));
    }

    // --- PreservesScale ---

    [Theory]
    [InlineData(ComparisonAggregation.Sum)]
    [InlineData(ComparisonAggregation.Count)]
    public void PreservesScale_SumAndCount_ReturnFalse(ComparisonAggregation mode)
    {
        Assert.False(ComparisonAggregator.PreservesScale(mode));
    }

    [Theory]
    [InlineData(ComparisonAggregation.First)]
    [InlineData(ComparisonAggregation.Average)]
    [InlineData(ComparisonAggregation.Max)]
    public void PreservesScale_FirstAverageMax_ReturnTrue(ComparisonAggregation mode)
    {
        Assert.True(ComparisonAggregator.PreservesScale(mode));
    }
}
