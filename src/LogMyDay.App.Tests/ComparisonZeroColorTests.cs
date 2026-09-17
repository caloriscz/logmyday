using LogMyDay.App.Services.Insights;
using LogMyDay.Domain.Constants;
using LogMyDay.Shared.Coloring;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.App.Tests;

/// <summary>
/// A logged zero must pick up its scheme colour in Timeline Comparison exactly like any other value.
/// Drives the real chain — aggregator, invariant colour key, scheme index, resolver — with a scheme
/// that covers zero, once as an exact entry and once as the low end of a band.
/// </summary>
public class ComparisonZeroColorTests
{
    private static readonly DateTime Day = new(2026, 9, 16);
    private const string Grey = "#9ca3af";
    private const string Green = "#22c55e";

    private static ColorSchemeIndex IndexWith(params ColorSchemeEntryResponse[] entries) => new(new[]
    {
        new ColorSchemeResponse { Id = 7, Name = "test", Entries = entries.ToList() }
    });

    [Theory]
    [InlineData(ComparisonAggregation.First)]
    [InlineData(ComparisonAggregation.Average)]
    [InlineData(ComparisonAggregation.Max)]
    public void Zero_MatchesExactZeroEntry(ComparisonAggregation mode)
    {
        var index = IndexWith(
            new ColorSchemeEntryResponse { Id = 1, RangeFrom = 0, RangeTo = 0, Color = Grey, SortOrder = 0 },
            new ColorSchemeEntryResponse { Id = 2, RangeFrom = 1, RangeTo = null, Color = Green, SortOrder = 1 });

        var cell = ComparisonAggregator.Aggregate(Day, ["0"], mode);

        Assert.Equal(ComparisonCellState.Value, cell.State);
        Assert.Equal("0", cell.ColorKey);
        Assert.Equal(Grey, index.ResolveColor(InputTypeIds.Integer, 7, cell.ColorKey));
    }

    [Fact]
    public void Zero_MatchesBandStartingAtZero()
    {
        var index = IndexWith(
            new ColorSchemeEntryResponse { Id = 1, RangeFrom = 0, RangeTo = 3, Color = Grey, SortOrder = 0 });

        var cell = ComparisonAggregator.Aggregate(Day, ["0"], ComparisonAggregation.First);

        Assert.Equal(Grey, index.ResolveColor(InputTypeIds.Integer, 7, cell.ColorKey));
    }

    [Fact]
    public void Zero_WithDecimalScale_StillMatches()
    {
        // "0.0" and "0.00" are how a Decimal tag may store a zero; the resolver must not care.
        var index = IndexWith(
            new ColorSchemeEntryResponse { Id = 1, RangeFrom = 0, RangeTo = 0, Color = Grey, SortOrder = 0 });

        foreach (var raw in new[] { "0.0", "0.00", "-0" })
        {
            var cell = ComparisonAggregator.Aggregate(Day, [raw], ComparisonAggregation.First);
            Assert.Equal(Grey, index.ResolveColor(InputTypeIds.Decimal, 7, cell.ColorKey));
        }
    }
}
