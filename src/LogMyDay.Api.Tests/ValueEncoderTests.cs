using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Tests;

/// <summary>Every input type encodes exactly as the web UI stores it; bad input says why.</summary>
public class ValueEncoderTests
{
    private static TagResponse Tag(int typeId, int? optionListId = null) => new() { Id = 1, Title = "t", TypeId = typeId, OptionListId = optionListId };

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

    private static string Encode(int typeId, string rawJson) => ValueEncoder.Encode(Tag(typeId), Json(rawJson));

    [Theory]
    [InlineData("12", "12")]
    [InlineData("\"-3\"", "-3")]
    [InlineData("\" 7 \"", "7")]
    public void Integer_AcceptsNumbersAndNumericStrings(string raw, string expected)
    {
        Assert.Equal(expected, Encode(InputTypeIds.Integer, raw));
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("\"abc\"")]
    [InlineData("true")]
    public void Integer_RejectsNonIntegral(string raw)
    {
        Assert.Throws<ArgumentException>(() => Encode(InputTypeIds.Integer, raw));
    }

    [Theory]
    [InlineData("72.5", "72.50")]
    [InlineData("\"3.14159\"", "3.14")]
    [InlineData("2", "2.00")]
    [InlineData("0.005", "0.01")]
    public void Decimal_StoresTwoDecimalsInvariant(string raw, string expected)
    {
        Assert.Equal(expected, Encode(InputTypeIds.Decimal, raw));
    }

    [Fact]
    public void Decimal_RejectsCommaDecimalSeparator()
    {
        var ex = Assert.Throws<ArgumentException>(() => Encode(InputTypeIds.Decimal, "\"72,5\""));

        Assert.Contains("dot", ex.Message);
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("\"yes\"", "true")]
    [InlineData("\"No\"", "false")]
    [InlineData("1", "true")]
    [InlineData("0", "false")]
    [InlineData("\"TRUE\"", "true")]
    public void Boolean_AcceptsTheUsualSpellings(string raw, string expected)
    {
        Assert.Equal(expected, Encode(InputTypeIds.Boolean, raw));
    }

    [Fact]
    public void Boolean_RejectsAnythingElse()
    {
        Assert.Throws<ArgumentException>(() => Encode(InputTypeIds.Boolean, "\"maybe\""));
        Assert.Throws<ArgumentException>(() => Encode(InputTypeIds.Boolean, "2"));
    }

    [Theory]
    [InlineData("\"2026-09-18\"", "2026-09-18")]
    [InlineData("\"2026-9-8\"", "2026-09-08")]
    public void Date_StoresIsoDate(string raw, string expected)
    {
        Assert.Equal(expected, Encode(InputTypeIds.Date, raw));
    }

    [Fact]
    public void Date_RejectsGarbage()
    {
        Assert.Throws<ArgumentException>(() => Encode(InputTypeIds.Date, "\"18.9.2026\""));
    }

    [Theory]
    [InlineData("\"07:05\"", "07:05")]
    [InlineData("\"7:05\"", "07:05")]
    [InlineData("\"23:59:30\"", "23:59")]
    public void Time_StoresHHmm(string raw, string expected)
    {
        Assert.Equal(expected, Encode(InputTypeIds.Time, raw));
    }

    [Fact]
    public void Time_RejectsTwelveHourClock()
    {
        Assert.Throws<ArgumentException>(() => Encode(InputTypeIds.Time, "\"7pm\""));
    }

    [Theory]
    [InlineData(InputTypeIds.StarRating, "5", "5")]
    [InlineData(InputTypeIds.StarRating10, "10", "10")]
    [InlineData(InputTypeIds.Percentage, "\"100\"", "100")]
    [InlineData(InputTypeIds.Score, "0", "0")]
    [InlineData(InputTypeIds.Score10, "7", "7")]
    public void RangedTypes_AcceptIntegersInRange(int typeId, string raw, string expected)
    {
        Assert.Equal(expected, Encode(typeId, raw));
    }

    [Theory]
    [InlineData(InputTypeIds.StarRating, "6")]
    [InlineData(InputTypeIds.StarRating10, "-1")]
    [InlineData(InputTypeIds.Percentage, "101")]
    [InlineData(InputTypeIds.Score, "2.5")]
    public void RangedTypes_RejectOutOfRangeOrFractional(int typeId, string raw)
    {
        Assert.Throws<ArgumentException>(() => Encode(typeId, raw));
    }

    [Fact]
    public void String_IsStoredTrimmed_AndScalarsAreStringified()
    {
        Assert.Equal("felt good", Encode(InputTypeIds.String, "\"  felt good \""));
        Assert.Equal("42", Encode(InputTypeIds.String, "42"));
    }

    [Fact]
    public void OptionList_AcceptsValueOrDisplayName_AndStoresTheValue()
    {
        var list = new TagOptionListResponse
        {
            Id = 9,
            Name = "Mood",
            Options = [new TagOptionResponse { Id = 1, Value = "happy", DisplayName = "Happy 😊" }, new TagOptionResponse { Id = 2, Value = "sad" }]
        };

        Assert.Equal("happy", ValueEncoder.Encode(Tag(InputTypeIds.String, 9), Json("\"Happy 😊\""), list));
        Assert.Equal("sad", ValueEncoder.Encode(Tag(InputTypeIds.String, 9), Json("\"SAD\""), list));

        var ex = Assert.Throws<ArgumentException>(() => ValueEncoder.Encode(Tag(InputTypeIds.String, 9), Json("\"angry\""), list));
        Assert.Contains("'happy', 'sad'", ex.Message);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[1]")]
    [InlineData("{\"a\":1}")]
    public void NonScalars_AreRejected(string raw)
    {
        Assert.Throws<ArgumentException>(() => Encode(InputTypeIds.String, raw));
    }

    [Fact]
    public void Describe_CoversEveryInputType()
    {
        foreach (var id in Enumerable.Range(1, 11))
        {
            Assert.False(string.IsNullOrWhiteSpace(ValueEncoder.Describe(id)));
        }
    }
}
