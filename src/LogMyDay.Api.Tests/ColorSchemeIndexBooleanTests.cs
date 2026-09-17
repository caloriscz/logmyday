using LogMyDay.Domain.Constants;
using LogMyDay.Shared.Coloring;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Calendar, Journal, Activities and the day dialog hand ColorSchemeIndex the raw stored value. For
/// a Boolean tag that is "true"/"false", and its scheme is written against 1 and 0 — the index must
/// bridge the two, and only for Boolean tags.
/// </summary>
public class ColorSchemeIndexBooleanTests
{
    private const string Bad = "#ef4444";
    private const string Good = "#22c55e";
    private const int SchemeId = 9;

    private static ColorSchemeIndex IndexWithExactOneAndZero() => new(new[]
    {
        new ColorSchemeResponse
        {
            Id = SchemeId,
            Name = "alcohol",
            Entries =
            [
                new ColorSchemeEntryResponse { Id = 1, RangeFrom = 1, RangeTo = 1, Color = Bad, SortOrder = 0 },
                new ColorSchemeEntryResponse { Id = 2, RangeFrom = 0, RangeTo = 0, Color = Good, SortOrder = 1 }
            ]
        }
    });

    [Theory]
    [InlineData("true", Bad)]
    [InlineData("false", Good)]
    [InlineData("True", Bad)]
    [InlineData("FALSE", Good)]
    [InlineData("1", Bad)]
    [InlineData("0", Good)]
    public void BooleanTag_RawValue_ResolvesAgainstOneAndZero(string raw, string expected)
    {
        var index = IndexWithExactOneAndZero();

        Assert.Equal(expected, index.ResolveColor(InputTypeIds.Boolean, SchemeId, raw));
    }

    [Fact]
    public void StringTag_HoldingTheWordTrue_ResolvesNothing()
    {
        // The word is only a truth value on a Boolean tag.
        var index = IndexWithExactOneAndZero();

        Assert.Null(index.ResolveColor(InputTypeIds.String, SchemeId, "true"));
    }

    [Fact]
    public void BooleanTag_WithoutScheme_ResolvesNothing()
    {
        // Falls through to BooleanDisplay's own green/red.
        Assert.Null(ColorSchemeIndex.Empty.ResolveColor(InputTypeIds.Boolean, null, "true"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("maybe")]
    [InlineData(null)]
    public void BooleanTag_UnreadableValue_ResolvesNothing(string? raw)
    {
        var index = IndexWithExactOneAndZero();

        Assert.Null(index.ResolveColor(InputTypeIds.Boolean, SchemeId, raw));
    }
}
