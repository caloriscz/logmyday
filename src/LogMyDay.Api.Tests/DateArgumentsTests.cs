using LogMyDay.Domain.Enums;
using LogMyDay.Mcp.Infrastructure;

namespace LogMyDay.Api.Tests;

/// <summary>Date arguments land on the stored naive-local convention whatever form the agent used.</summary>
public class DateArgumentsTests
{
    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    [Theory]
    [InlineData("2026-09-18", 2026, 9, 18)]
    [InlineData(" 2026-01-05 ", 2026, 1, 5)]
    [InlineData("2026-09-18T14:30", 2026, 9, 18)]
    public void ParseDate_AcceptsIsoDates_AndTakesTheDatePartOfADateTime(string text, int y, int m, int d)
    {
        Assert.Equal(new DateOnly(y, m, d), DateArguments.ParseDate(text, "from"));
    }

    [Theory]
    [InlineData("18.9.2026")]
    [InlineData("tomorrow")]
    [InlineData("2026/09/18")]
    public void ParseDate_RejectsOtherForms_NamingTheArgument(string text)
    {
        var ex = Assert.Throws<ArgumentException>(() => DateArguments.ParseDate(text, "from"));

        Assert.StartsWith("from ", ex.Message);
    }

    [Theory]
    [InlineData("2026-09-18T14:30", "2026-09-18T14:30:00")]
    [InlineData("2026-09-18 14:30:15", "2026-09-18T14:30:15")]
    [InlineData("2026-09-18", "2026-09-18T00:00:00")]
    public void ParseDateTime_NaiveForms_AreTakenAsLocal(string text, string expected)
    {
        var value = DateArguments.ParseDateTime(text, "dateTime", Vienna);

        Assert.Equal(DateTime.Parse(expected), value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Theory]
    [InlineData("2026-07-01T12:00:00Z", "2026-07-01T14:00:00")]
    [InlineData("2026-01-01T12:00:00+05:00", "2026-01-01T08:00:00")]
    public void ParseDateTime_WithOffset_IsConvertedIntoTheUsersZone(string text, string expected)
    {
        var value = DateArguments.ParseDateTime(text, "dateTime", Vienna);

        Assert.Equal(DateTime.Parse(expected), value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Fact]
    public void EndOfDay_IsTheLastTick()
    {
        Assert.Equal(new DateTime(2026, 9, 19).AddTicks(-1), DateArguments.EndOfDay(new DateOnly(2026, 9, 18)));
    }

    [Fact]
    public void PeriodOf_MatchesTheActivityServiceRules()
    {
        var at = new DateTime(2026, 9, 16, 14, 45, 0); // a Wednesday

        Assert.Equal((new DateTime(2026, 9, 16), new DateTime(2026, 9, 17).AddTicks(-1)), DateArguments.PeriodOf(TimeGranularity.Daily, at));
        Assert.Equal((new DateTime(2026, 9, 16, 14, 0, 0), new DateTime(2026, 9, 16, 15, 0, 0).AddTicks(-1)), DateArguments.PeriodOf(TimeGranularity.Hourly, at));
        Assert.Equal((new DateTime(2026, 9, 14), new DateTime(2026, 9, 21).AddTicks(-1)), DateArguments.PeriodOf(TimeGranularity.Weekly, at));
        Assert.Equal((new DateTime(2026, 9, 1), new DateTime(2026, 10, 1).AddTicks(-1)), DateArguments.PeriodOf(TimeGranularity.Monthly, at));
        Assert.Equal((new DateTime(2026, 1, 1), new DateTime(2027, 1, 1).AddTicks(-1)), DateArguments.PeriodOf(TimeGranularity.Yearly, at));
        Assert.Equal((at, at), DateArguments.PeriodOf(TimeGranularity.Exact, at));
    }
}
