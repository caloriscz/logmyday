using System.Globalization;
using LogMyDay.App.Services.Insights;
using LogMyDay.Domain.Constants;
using LogMyDay.Shared.Coloring;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Tests;

/// <summary>
/// Boolean tags in Timeline Comparison: "true"/"false" read as 1/0 so every aggregation means
/// something, First/Max render as a glyph, Average as a share, and a scheme's Exact 1 / Exact 0
/// entries colour them. The boolean reading is per tag, never inferred from the text.
/// </summary>
public class ComparisonBooleanTests
{
    private static readonly DateTime Day = new(2026, 9, 16);
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // --- Parsing is opt-in per tag ---

    [Theory]
    [InlineData("true", 1)]
    [InlineData("false", 0)]
    [InlineData("True", 1)]
    [InlineData("FALSE", 0)]
    [InlineData("1", 1)]
    [InlineData("0", 0)]
    public void BooleanTag_ReadsTruthValues(string raw, int expected)
    {
        var cell = ComparisonAggregator.Aggregate(Day, [raw], ComparisonAggregation.First, isBoolean: true);

        Assert.Equal(ComparisonCellState.Value, cell.State);
        Assert.Equal(expected, cell.NumericValue);
    }

    [Fact]
    public void StringTag_ContainingTheWordTrue_StaysText()
    {
        // The flag is per tag: a String tag holding "true" must not silently become the number 1.
        var cell = ComparisonAggregator.Aggregate(Day, ["true"], ComparisonAggregation.First);

        Assert.Null(cell.NumericValue);
        Assert.Equal("true", ComparisonAggregator.Format(cell, ComparisonAggregation.First, Invariant));
    }

    [Fact]
    public void StringTag_ContainingTheWordTrue_IsNotApplicableUnderSum()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["true", "false"], ComparisonAggregation.Sum);

        Assert.Equal(ComparisonCellState.NotApplicable, cell.State);
    }

    // --- Each aggregation on a boolean day ---

    [Fact]
    public void First_TakesTheFirstEntry()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["false", "true"], ComparisonAggregation.First, isBoolean: true);

        Assert.Equal(0, cell.NumericValue);
    }

    [Theory]
    [InlineData(new[] { "false", "true", "false" }, 1)]
    [InlineData(new[] { "false", "false" }, 0)]
    public void Max_IsAnyTrueThatDay(string[] raw, int expected)
    {
        var cell = ComparisonAggregator.Aggregate(Day, raw, ComparisonAggregation.Max, isBoolean: true);

        Assert.Equal(expected, cell.NumericValue);
    }

    [Fact]
    public void Sum_CountsTrues()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["true", "false", "true"], ComparisonAggregation.Sum, isBoolean: true);

        Assert.Equal(2, cell.NumericValue);
    }

    [Fact]
    public void Count_CountsEntries()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["true", "false", "true"], ComparisonAggregation.Count, isBoolean: true);

        Assert.Equal(3, cell.NumericValue);
    }

    [Fact]
    public void Average_IsTheShareOfTrue()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["true", "true", "false"], ComparisonAggregation.Average, isBoolean: true);

        Assert.NotNull(cell.NumericValue);
        Assert.Equal(2m / 3m, cell.NumericValue.Value, precision: 10);
    }

    // --- Display ---

    [Theory]
    [InlineData(ComparisonAggregation.First, "true", ComparisonAggregator.TrueGlyph)]
    [InlineData(ComparisonAggregation.First, "false", ComparisonAggregator.FalseGlyph)]
    [InlineData(ComparisonAggregation.Max, "true", ComparisonAggregator.TrueGlyph)]
    public void FirstAndMax_FormatAsGlyph(ComparisonAggregation mode, string raw, string expected)
    {
        var cell = ComparisonAggregator.Aggregate(Day, [raw], mode, isBoolean: true);

        Assert.Equal(expected, ComparisonAggregator.Format(cell, mode, Invariant, isBoolean: true));
    }

    [Fact]
    public void Average_FormatsAsPercentage_InDisplayCulture()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["true", "true", "false"], ComparisonAggregation.Average, isBoolean: true);

        Assert.Equal("67 %", ComparisonAggregator.Format(cell, ComparisonAggregation.Average, Invariant, isBoolean: true));
        Assert.Equal("67 %", ComparisonAggregator.Format(cell, ComparisonAggregation.Average, new CultureInfo("cs-CZ"), isBoolean: true));
    }

    [Fact]
    public void Average_OnNumericTag_IsUnchanged()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["1", "2"], ComparisonAggregation.Average);

        Assert.Equal("1.5", ComparisonAggregator.Format(cell, ComparisonAggregation.Average, Invariant));
    }

    [Theory]
    [InlineData(ComparisonAggregation.First, true)]
    [InlineData(ComparisonAggregation.Max, true)]
    [InlineData(ComparisonAggregation.Average, false)]
    [InlineData(ComparisonAggregation.Sum, false)]
    [InlineData(ComparisonAggregation.Count, false)]
    public void Icon_OnlyForSingleTruthValueModes(ComparisonAggregation mode, bool expected)
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["true"], mode, isBoolean: true);

        Assert.Equal(expected, ComparisonAggregator.ShowsBooleanIcon(cell, mode, isBoolean: true));
    }

    [Fact]
    public void Icon_NeverForNumericTag()
    {
        var cell = ComparisonAggregator.Aggregate(Day, ["1"], ComparisonAggregation.First);

        Assert.False(ComparisonAggregator.ShowsBooleanIcon(cell, ComparisonAggregation.First, isBoolean: false));
    }

    // --- Colour ---

    [Fact]
    public void Scheme_ExactOneAndZero_ColourBooleanCells()
    {
        const string Bad = "#ef4444";
        const string Good = "#22c55e";

        var index = new ColorSchemeIndex(new[]
        {
            new ColorSchemeResponse
            {
                Id = 3,
                Name = "alcohol",
                Entries =
                [
                    new ColorSchemeEntryResponse { Id = 1, RangeFrom = 1, RangeTo = 1, Color = Bad, SortOrder = 0 },
                    new ColorSchemeEntryResponse { Id = 2, RangeFrom = 0, RangeTo = 0, Color = Good, SortOrder = 1 }
                ]
            }
        });

        var yes = ComparisonAggregator.Aggregate(Day, ["true"], ComparisonAggregation.First, isBoolean: true);
        var no = ComparisonAggregator.Aggregate(Day, ["false"], ComparisonAggregation.First, isBoolean: true);

        // A tag where true is bad gets red for true — the scheme, not the icon's fixed colours, decides.
        Assert.Equal(Bad, index.ResolveColor(InputTypeIds.Boolean, 3, yes.ColorKey));
        Assert.Equal(Good, index.ResolveColor(InputTypeIds.Boolean, 3, no.ColorKey));
    }

    [Fact]
    public void NoScheme_BooleanCellHasNoBadgeColour()
    {
        // Falls through to the icon, which paints itself.
        var cell = ComparisonAggregator.Aggregate(Day, ["true"], ComparisonAggregation.First, isBoolean: true);

        Assert.Null(ColorSchemeIndex.Empty.ResolveColor(InputTypeIds.Boolean, null, cell.ColorKey));
    }
}
