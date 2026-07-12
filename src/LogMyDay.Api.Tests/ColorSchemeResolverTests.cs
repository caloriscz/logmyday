using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Helpers;

namespace LogMyDay.Api.Tests;

public class ColorSchemeResolverTests
{
    private const string Red = "#ef4444";
    private const string Amber = "#eab308";
    private const string Green = "#22c55e";

    // --- Direction-aware defaults (no assigned scheme) ---

    [Theory]
    [InlineData(InputTypeIds.StarRating, 5, Green)]   // higher = better
    [InlineData(InputTypeIds.StarRating, 0, Red)]
    [InlineData(InputTypeIds.StarRating10, 10, Green)]
    [InlineData(InputTypeIds.Percentage, 100, Green)]
    [InlineData(InputTypeIds.Percentage, 0, Red)]
    [InlineData(InputTypeIds.Percentage, 50, Amber)]
    [InlineData(InputTypeIds.Score, 5, Red)]          // lower = better
    [InlineData(InputTypeIds.Score, 0, Green)]
    [InlineData(InputTypeIds.Score10, 0, Green)]
    public void Resolve_NoScheme_UsesDirectionAwareDefault(int inputTypeId, double value, string expected)
    {
        var color = ColorSchemeResolver.Resolve(inputTypeId, null, value);

        Assert.Equal(expected, color);
    }

    [Theory]
    [InlineData(InputTypeIds.Integer)]
    [InlineData(InputTypeIds.Decimal)]
    [InlineData(InputTypeIds.String)]
    [InlineData(InputTypeIds.Boolean)]
    public void Resolve_NoScheme_UnscaledTypes_ReturnNull(int inputTypeId)
    {
        Assert.Null(ColorSchemeResolver.Resolve(inputTypeId, null, 42));
    }

    // --- Assigned scheme: exact values ---

    [Fact]
    public void Resolve_AssignedExactValues_MatchesExactEntry()
    {
        var entries = new List<ColorSchemeEntry>
        {
            Exact(1, Red, 0),
            Exact(5, Green, 1)
        };

        Assert.Equal(Green, ColorSchemeResolver.Resolve(InputTypeIds.StarRating, entries, 5));
        Assert.Equal(Red, ColorSchemeResolver.Resolve(InputTypeIds.StarRating, entries, 1));
    }

    [Fact]
    public void Resolve_AssignedScheme_NoMatch_ReturnsNull()
    {
        var entries = new List<ColorSchemeEntry> { Exact(1, Red, 0), Exact(5, Green, 1) };

        Assert.Null(ColorSchemeResolver.Resolve(InputTypeIds.StarRating, entries, 3));
    }

    // --- Assigned scheme: ranges and open bounds ---

    [Theory]
    [InlineData(15, Red)]
    [InlineData(45, Amber)]
    [InlineData(90, Green)]   // open upper bound
    public void Resolve_AssignedRanges_MatchesBand(double value, string expected)
    {
        var entries = new List<ColorSchemeEntry>
        {
            Band(0, 30, Red, 0),
            Band(30, 70, Amber, 1),
            Band(70, null, Green, 2)   // >= 70
        };

        Assert.Equal(expected, ColorSchemeResolver.Resolve(InputTypeIds.Percentage, entries, value));
    }

    [Fact]
    public void Resolve_AssignedScheme_OverridesDefault()
    {
        // Star default would be green at 5; assigned scheme forces red.
        var entries = new List<ColorSchemeEntry> { Band(null, null, Red, 0) };

        Assert.Equal(Red, ColorSchemeResolver.Resolve(InputTypeIds.StarRating, entries, 5));
    }

    [Fact]
    public void Resolve_OverlappingEntries_LowestSortOrderWins()
    {
        var entries = new List<ColorSchemeEntry>
        {
            Band(0, 100, Green, 1),
            Band(0, 100, Red, 0)
        };

        Assert.Equal(Red, ColorSchemeResolver.Resolve(InputTypeIds.Percentage, entries, 50));
    }

    private static ColorSchemeEntry Exact(double value, string color, int sort) =>
        new() { RangeFrom = value, RangeTo = value, Color = color, SortOrder = sort };

    private static ColorSchemeEntry Band(double? from, double? to, string color, int sort) =>
        new() { RangeFrom = from, RangeTo = to, Color = color, SortOrder = sort };
}
