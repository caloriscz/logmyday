using LogMyDay.Shared.Scanning;

namespace LogMyDay.Api.Tests;

public class LmdQrCodeParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrWhitespace_ReturnsNotAppFormatted(string? input)
    {
        var result = LmdQrCodeParser.Parse(input);

        Assert.False(result.IsAppFormatted);
        Assert.Null(result.TagId);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("https://example.com")]
    [InlineData("random text")]
    [InlineData("LMD:tag/5")]
    public void Parse_NonLmdUri_ReturnsNotAppFormatted(string input)
    {
        var result = LmdQrCodeParser.Parse(input);

        Assert.False(result.IsAppFormatted);
    }

    [Fact]
    public void Parse_TagIdOnly_ReturnsTagId()
    {
        var result = LmdQrCodeParser.Parse("lmd://tag/42");

        Assert.True(result.IsAppFormatted);
        Assert.Equal(42, result.TagId);
        Assert.Null(result.Value);
        Assert.Null(result.DisplayName);
    }

    [Fact]
    public void Parse_CaseInsensitiveScheme_Works()
    {
        var result = LmdQrCodeParser.Parse("LMD://TAG/7");

        Assert.True(result.IsAppFormatted);
        Assert.Equal(7, result.TagId);
    }

    [Fact]
    public void Parse_TagIdWithValue_ReturnsBoth()
    {
        var result = LmdQrCodeParser.Parse("lmd://tag/10?v=400");

        Assert.True(result.IsAppFormatted);
        Assert.Equal(10, result.TagId);
        Assert.Equal("400", result.Value);
        Assert.Null(result.DisplayName);
    }

    [Fact]
    public void Parse_TagIdWithValueAndName_ReturnsAll()
    {
        var result = LmdQrCodeParser.Parse("lmd://tag/5?v=300&n=Morning+Run");

        Assert.True(result.IsAppFormatted);
        Assert.Equal(5, result.TagId);
        Assert.Equal("300", result.Value);
        Assert.Equal("Morning Run", result.DisplayName);
    }

    [Fact]
    public void Parse_UrlEncodedParams_DecodesCorrectly()
    {
        var result = LmdQrCodeParser.Parse("lmd://tag/3?v=hello%20world&n=Test%26Value");

        Assert.True(result.IsAppFormatted);
        Assert.Equal(3, result.TagId);
        Assert.Equal("hello world", result.Value);
        Assert.Equal("Test&Value", result.DisplayName);
    }

    [Fact]
    public void Parse_NameOnly_ReturnsNameWithoutValue()
    {
        var result = LmdQrCodeParser.Parse("lmd://tag/8?n=Label");

        Assert.True(result.IsAppFormatted);
        Assert.Equal(8, result.TagId);
        Assert.Null(result.Value);
        Assert.Equal("Label", result.DisplayName);
    }

    [Theory]
    [InlineData("lmd://tag/0")]
    [InlineData("lmd://tag/-1")]
    [InlineData("lmd://tag/abc")]
    [InlineData("lmd://tag/")]
    public void Parse_InvalidTagId_ReturnsNotAppFormatted(string input)
    {
        var result = LmdQrCodeParser.Parse(input);

        Assert.False(result.IsAppFormatted);
    }

    [Fact]
    public void Parse_UnknownSegment_ReturnsNotAppFormatted()
    {
        var result = LmdQrCodeParser.Parse("lmd://unknown/42");

        Assert.False(result.IsAppFormatted);
    }

    [Fact]
    public void Parse_EmptyQueryString_ReturnsTagIdOnly()
    {
        var result = LmdQrCodeParser.Parse("lmd://tag/15?");

        Assert.True(result.IsAppFormatted);
        Assert.Equal(15, result.TagId);
        Assert.Null(result.Value);
        Assert.Null(result.DisplayName);
    }
}
