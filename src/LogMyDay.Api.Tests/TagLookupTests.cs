using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Tests;

/// <summary>The CLI resolver semantics, case by case, plus the candidate list the MCP adds.</summary>
public class TagLookupTests
{
    private static TagResponse Tag(int id, string name, string? group = null, int? groupId = null) => new()
    {
        Id = id,
        Title = group == null ? name : $"{group}: {name}",
        GroupName = group,
        GroupId = group == null ? null : groupId ?? id * 10
    };

    private static readonly List<TagResponse> Tags =
    [
        Tag(1, "Vitamin D", "Health"),
        Tag(2, "Vitamin C", "Health"),
        Tag(3, "Vitamin D"),
        Tag(4, "Sleep"),
        Tag(5, "Sleep quality"),
        Tag(6, "Mood")
    ];

    [Fact]
    public void NumericId_IsExact()
    {
        Assert.Equal(4, TagLookup.Resolve(Tags, "4").Match?.Id);
        Assert.Null(TagLookup.Resolve(Tags, "99").Match);
    }

    [Fact]
    public void ColonPrefix_MeansUngroupedExactName()
    {
        Assert.Equal(3, TagLookup.Resolve(Tags, ":Vitamin D").Match?.Id);
        Assert.Null(TagLookup.Resolve(Tags, ":Vitamin C").Match);
    }

    [Fact]
    public void GroupColonName_IsExactQualifiedTitle_NoFuzzyFallback()
    {
        Assert.Equal(2, TagLookup.Resolve(Tags, "Health:Vitamin C").Match?.Id);
        Assert.Equal(2, TagLookup.Resolve(Tags, "health : vitamin c").Match?.Id);
        Assert.Null(TagLookup.Resolve(Tags, "Health:Vit").Match);
    }

    [Fact]
    public void PlainName_ExactWins_CaseInsensitive()
    {
        Assert.Equal(4, TagLookup.Resolve(Tags, "sleep").Match?.Id);
        Assert.Equal(6, TagLookup.Resolve(Tags, "MOOD").Match?.Id);
    }

    [Fact]
    public void PlainName_MatchesTheBareNameOfAGroupedTag()
    {
        // "Vitamin C" is grouped; its qualified title is "Health: Vitamin C".
        Assert.Equal(2, TagLookup.Resolve(Tags, "Vitamin C").Match?.Id);
    }

    [Fact]
    public void PlainName_UniqueStartsWith_Matches()
    {
        Assert.Equal(6, TagLookup.Resolve(Tags, "Mo").Match?.Id);
    }

    [Fact]
    public void PlainName_UniqueContains_Matches()
    {
        Assert.Equal(5, TagLookup.Resolve(Tags, "quality").Match?.Id);
    }

    [Fact]
    public void PlainName_Ambiguous_ReturnsCandidatesAndNoMatch()
    {
        var result = TagLookup.Resolve(Tags, "Vitamin");

        Assert.Null(result.Match);
        Assert.True(result.IsAmbiguous);
        Assert.Equal([1, 2, 3], result.Candidates.Select(c => c.Id).OrderBy(i => i));
    }

    [Fact]
    public void ExactBeatsStartsWith_WhenBothExist()
    {
        // "Sleep" is exact even though "Sleep quality" also starts with it.
        Assert.Equal(4, TagLookup.Resolve(Tags, "Sleep").Match?.Id);
    }

    [Fact]
    public void NoMatch_ReturnsEmptyCandidates()
    {
        var result = TagLookup.Resolve(Tags, "zzz");

        Assert.Null(result.Match);
        Assert.Empty(result.Candidates);
        Assert.False(result.IsAmbiguous);
    }

    [Fact]
    public void EmptyQuery_MatchesNothing()
    {
        Assert.Null(TagLookup.Resolve(Tags, "  ").Match);
    }
}
