using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Tests;

/// <summary>The summary maths, the bucketing convention and the two streak rules, as the plan states them.</summary>
public class ActivitySummaryServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private sealed class Fixture : IDisposable
    {
        public LogMyDayDbContext Context { get; }
        public ActivitySummaryService Service { get; }

        public Fixture(string name, string culture = "en-US")
        {
            var options = new DbContextOptionsBuilder<LogMyDayDbContext>().UseInMemoryDatabase(name).Options;
            Context = new LogMyDayDbContext(options);
            Context.Users.Add(new User { Id = UserId, Email = "s@test.com", PasswordHash = "x", Culture = culture });
            Context.Users.Add(new User { Id = OtherUserId, Email = "o@test.com", PasswordHash = "x" });
            Context.SaveChanges();
            Service = new ActivitySummaryService(Context);
        }

        public Tag Tag(int inputTypeId, int? optionListId = null, Guid? owner = null)
        {
            var tag = new Tag { TagName = $"t{inputTypeId}", UserId = owner ?? UserId, InputTypeId = inputTypeId, OptionListId = optionListId, TimeGranularity = TimeGranularity.Daily };
            Context.Tags.Add(tag);
            Context.SaveChanges();

            return tag;
        }

        public void Log(Tag tag, string date, string value, string time = "10:00")
        {
            Context.Activities.Add(new Activity { TagId = tag.Id, UserId = tag.UserId, DateStarted = DateTime.Parse($"{date}T{time}"), Description = value });
            Context.SaveChanges();
        }

        public void Dispose() => Context.Dispose();
    }

    private static ActivitySummaryRequest Request(Tag tag, string from, string to, SummaryBucket bucket = SummaryBucket.Day) => new()
    {
        TagId = tag.Id,
        From = DateOnly.Parse(from),
        To = DateOnly.Parse(to),
        Bucket = bucket
    };

    [Fact]
    public async Task Numeric_SumMinMaxAverage_FirstLast_AndInvalidRowsExcludedFromMaths()
    {
        using var f = new Fixture(nameof(Numeric_SumMinMaxAverage_FirstLast_AndInvalidRowsExcludedFromMaths));
        var tag = f.Tag(InputTypeIds.Integer);
        f.Log(tag, "2026-03-01", "10");
        f.Log(tag, "2026-03-01", "20", "18:00");
        f.Log(tag, "2026-03-03", "30");
        f.Log(tag, "2026-03-04", "oops");

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-05"), UserId);

        Assert.Equal("numeric", s.ValueKind);
        Assert.Equal(4, s.Count);
        Assert.Equal(1, s.InvalidValueCount);
        Assert.Equal(60, s.Sum);
        Assert.Equal(10, s.Min);
        Assert.Equal(30, s.Max);
        Assert.Equal(20, s.Average);
        Assert.Equal(3, s.DaysWithData);
        Assert.Equal(5, s.DaysInRange);
        Assert.Equal("10", s.First!.Value);
        Assert.Equal("oops", s.Last!.Value);
        Assert.Equal(ActivitySummaryResponse.StoredLocalDate, s.Convention);
    }

    [Fact]
    public async Task Numeric_AcceptsACommaDecimal_FromOlderRows()
    {
        using var f = new Fixture(nameof(Numeric_AcceptsACommaDecimal_FromOlderRows));
        var tag = f.Tag(InputTypeIds.Decimal);
        f.Log(tag, "2026-03-01", "72,5");
        f.Log(tag, "2026-03-02", "73.5");

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-02"), UserId);

        Assert.Equal(0, s.InvalidValueCount);
        Assert.Equal(146, s.Sum);
    }

    [Fact]
    public async Task Boolean_SumIsTrueCount_AverageIsTrueShare_AndOnlyTrueDaysCountAsData()
    {
        using var f = new Fixture(nameof(Boolean_SumIsTrueCount_AverageIsTrueShare_AndOnlyTrueDaysCountAsData));
        var tag = f.Tag(InputTypeIds.Boolean);
        f.Log(tag, "2026-03-01", "true");
        f.Log(tag, "2026-03-02", "false");
        f.Log(tag, "2026-03-03", "true");
        f.Log(tag, "2026-03-04", "yes");

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-04"), UserId);

        Assert.Equal("boolean", s.ValueKind);
        Assert.Equal(3, s.Sum);
        Assert.Equal(0.75, s.Average);
        Assert.Null(s.Min);
        Assert.Equal(3, s.DaysWithData);
        Assert.Equal(2, s.CurrentStreak); // 03 and 04; 02 was false
    }

    [Fact]
    public async Task Categorical_AndText_GetFrequenciesWithShares()
    {
        using var f = new Fixture(nameof(Categorical_AndText_GetFrequenciesWithShares));
        var categorical = f.Tag(InputTypeIds.String, optionListId: 1);
        f.Log(categorical, "2026-03-01", "happy");
        f.Log(categorical, "2026-03-02", "sad");
        f.Log(categorical, "2026-03-03", "happy");
        f.Log(categorical, "2026-03-04", "");

        var s = await f.Service.Summarize(Request(categorical, "2026-03-01", "2026-03-04"), UserId);

        Assert.Equal("categorical", s.ValueKind);
        Assert.Equal(1, s.InvalidValueCount);
        Assert.Null(s.Sum);
        var top = Assert.Single(s.Frequencies!, x => x.Value == "happy");
        Assert.Equal(2, top.Count);
        Assert.Equal(2.0 / 3, top.Share, 6);
        Assert.Equal("happy", s.Frequencies![0].Value);

        var text = f.Tag(InputTypeIds.String);
        f.Log(text, "2026-03-01", "walked");
        Assert.Equal("text", (await f.Service.Summarize(Request(text, "2026-03-01", "2026-03-01"), UserId)).ValueKind);
    }

    [Fact]
    public async Task DateAndTime_KindsCountRows_AndFlagUnreadableValues()
    {
        using var f = new Fixture(nameof(DateAndTime_KindsCountRows_AndFlagUnreadableValues));
        var date = f.Tag(InputTypeIds.Date);
        f.Log(date, "2026-03-01", "2026-02-28");
        f.Log(date, "2026-03-02", "yesterday");
        var time = f.Tag(InputTypeIds.Time);
        f.Log(time, "2026-03-01", "07:30");

        var d = await f.Service.Summarize(Request(date, "2026-03-01", "2026-03-02"), UserId);
        var t = await f.Service.Summarize(Request(time, "2026-03-01", "2026-03-01"), UserId);

        Assert.Equal("date", d.ValueKind);
        Assert.Equal(1, d.InvalidValueCount);
        Assert.Equal("time", t.ValueKind);
        Assert.Equal(0, t.InvalidValueCount);
        Assert.Null(t.Frequencies);
    }

    [Fact]
    public async Task DayBuckets_CoverTheWholeRange_IncludingEmptyDays()
    {
        using var f = new Fixture(nameof(DayBuckets_CoverTheWholeRange_IncludingEmptyDays));
        var tag = f.Tag(InputTypeIds.Integer);
        f.Log(tag, "2026-03-02", "5");
        f.Log(tag, "2026-03-02", "7", "20:00");

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-03"), UserId);

        Assert.Equal(3, s.Buckets.Count);
        Assert.Equal(0, s.Buckets[0].Count);
        Assert.Null(s.Buckets[0].Sum);
        Assert.Equal(2, s.Buckets[1].Count);
        Assert.Equal(12, s.Buckets[1].Sum);
        Assert.Equal(6, s.Buckets[1].Average);
        Assert.Equal(1, s.Buckets[1].DaysWithData);
        Assert.Equal(new DateOnly(2026, 3, 3), s.Buckets[2].Start);
        Assert.Equal(new DateOnly(2026, 3, 3), s.Buckets[2].End);
    }

    [Fact]
    public async Task Bucketing_UsesTheStoredDate_WithNoTimeZoneShift()
    {
        using var f = new Fixture(nameof(Bucketing_UsesTheStoredDate_WithNoTimeZoneShift));
        var tag = f.Tag(InputTypeIds.Integer);
        f.Log(tag, "2026-03-01", "1", "23:30"); // late evening stays on the 1st

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-02"), UserId);

        Assert.Equal(1, s.Buckets[0].Count);
        Assert.Equal(0, s.Buckets[1].Count);
    }

    [Theory]
    [InlineData("en-US", DayOfWeek.Sunday, "2026-03-01")] // 2026-03-04 is a Wednesday; US weeks start Sunday
    [InlineData("de-DE", DayOfWeek.Monday, "2026-03-02")]
    public async Task WeekBuckets_StartOnTheCulturesFirstDay_FirstBucketStartsAtFrom(string culture, DayOfWeek expectedStart, string secondBucketExpectedEnd)
    {
        using var f = new Fixture(nameof(WeekBuckets_StartOnTheCulturesFirstDay_FirstBucketStartsAtFrom) + culture, culture);
        var tag = f.Tag(InputTypeIds.Integer);
        f.Log(tag, "2026-03-04", "1");

        // From is a Saturday (2026-02-28); the first bucket is partial and ends the day before the week start.
        var s = await f.Service.Summarize(Request(tag, "2026-02-28", "2026-03-14", SummaryBucket.Week), UserId);

        Assert.Equal(expectedStart, s.WeekStartsOn);
        Assert.Equal(new DateOnly(2026, 2, 28), s.Buckets[0].Start);
        Assert.Equal(DateOnly.Parse(secondBucketExpectedEnd).AddDays(-1), s.Buckets[0].End);
        Assert.Equal(DateOnly.Parse(secondBucketExpectedEnd), s.Buckets[1].Start);
        Assert.Equal(1, s.Buckets[1].Count);
        Assert.Equal(new DateOnly(2026, 3, 14), s.Buckets[^1].End);
        Assert.All(s.Buckets.Skip(1), b => Assert.Equal(expectedStart, b.Start.DayOfWeek));
    }

    [Fact]
    public async Task MonthBuckets_AlignToCalendarMonths()
    {
        using var f = new Fixture(nameof(MonthBuckets_AlignToCalendarMonths));
        var tag = f.Tag(InputTypeIds.Integer);
        f.Log(tag, "2026-02-10", "1");
        f.Log(tag, "2026-03-10", "2");

        var s = await f.Service.Summarize(Request(tag, "2026-01-15", "2026-03-20", SummaryBucket.Month), UserId);

        Assert.Equal(3, s.Buckets.Count);
        Assert.Equal((new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 31)), (s.Buckets[0].Start, s.Buckets[0].End));
        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), (s.Buckets[1].Start, s.Buckets[1].End));
        Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 20)), (s.Buckets[2].Start, s.Buckets[2].End));
        Assert.Equal(1, s.Buckets[1].Sum);
        Assert.Equal(2, s.Buckets[2].Sum);
    }

    [Fact]
    public async Task CurrentStreak_EndsAtTo_WhenToHasData()
    {
        using var f = new Fixture(nameof(CurrentStreak_EndsAtTo_WhenToHasData));
        var tag = f.Tag(InputTypeIds.Integer);
        foreach (var day in new[] { "2026-03-03", "2026-03-04", "2026-03-05" })
        {
            f.Log(tag, day, "1");
        }

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-05"), UserId);

        Assert.Equal(3, s.CurrentStreak);
        Assert.Equal(new DateOnly(2026, 3, 5), s.CurrentStreakEnd);
        Assert.Equal(ActivitySummaryResponse.DayUnit, s.StreakUnit);
    }

    [Fact]
    public async Task CurrentStreak_EndsAtToMinusOne_WhenToHasNothingYet()
    {
        using var f = new Fixture(nameof(CurrentStreak_EndsAtToMinusOne_WhenToHasNothingYet));
        var tag = f.Tag(InputTypeIds.Integer);
        f.Log(tag, "2026-03-03", "1");
        f.Log(tag, "2026-03-04", "1");

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-05"), UserId);

        Assert.Equal(2, s.CurrentStreak);
        Assert.Equal(new DateOnly(2026, 3, 4), s.CurrentStreakEnd);
    }

    [Fact]
    public async Task CurrentStreak_IsZero_WhenNeitherToNorTheDayBeforeHasData()
    {
        using var f = new Fixture(nameof(CurrentStreak_IsZero_WhenNeitherToNorTheDayBeforeHasData));
        var tag = f.Tag(InputTypeIds.Integer);
        f.Log(tag, "2026-03-02", "1");

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-05"), UserId);

        Assert.Equal(0, s.CurrentStreak);
        Assert.Null(s.CurrentStreakEnd);
        Assert.Equal(1, s.LongestStreak);
    }

    [Fact]
    public async Task LongestStreak_HasStartAndEnd_WithinTheRange()
    {
        using var f = new Fixture(nameof(LongestStreak_HasStartAndEnd_WithinTheRange));
        var tag = f.Tag(InputTypeIds.Integer);
        foreach (var day in new[] { "2026-03-01", "2026-03-02", "2026-03-05", "2026-03-06", "2026-03-07", "2026-03-10" })
        {
            f.Log(tag, day, "1");
        }

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-10"), UserId);

        Assert.Equal(3, s.LongestStreak);
        Assert.Equal(new DateOnly(2026, 3, 5), s.LongestStreakStart);
        Assert.Equal(new DateOnly(2026, 3, 7), s.LongestStreakEnd);
        Assert.Equal(1, s.CurrentStreak);
    }

    [Fact]
    public async Task RowsOutsideTheRange_AndOtherUsersTags_AreIgnored()
    {
        using var f = new Fixture(nameof(RowsOutsideTheRange_AndOtherUsersTags_AreIgnored));
        var tag = f.Tag(InputTypeIds.Integer);
        f.Log(tag, "2026-02-28", "100", "23:59");
        f.Log(tag, "2026-03-01", "1");
        f.Log(tag, "2026-03-03", "100", "00:00");
        var foreign = f.Tag(InputTypeIds.Integer, owner: OtherUserId);

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-02"), UserId);

        Assert.Equal(1, s.Count);
        Assert.Equal(1, s.Sum);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Service.Summarize(Request(foreign, "2026-03-01", "2026-03-02"), UserId));
    }

    [Fact]
    public async Task RangeLimits_PerBucket_WithHints()
    {
        using var f = new Fixture(nameof(RangeLimits_PerBucket_WithHints));
        var tag = f.Tag(InputTypeIds.Integer);

        var day = await Assert.ThrowsAsync<ArgumentException>(() => f.Service.Summarize(Request(tag, "2025-01-01", "2026-03-01"), UserId));
        Assert.Contains("Week", day.Message);
        Assert.NotNull(await f.Service.Summarize(Request(tag, "2025-01-01", "2026-03-01", SummaryBucket.Week), UserId));

        var week = await Assert.ThrowsAsync<ArgumentException>(() => f.Service.Summarize(Request(tag, "2020-01-01", "2026-03-01", SummaryBucket.Week), UserId));
        Assert.Contains("Month", week.Message);

        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.Summarize(Request(tag, "2026-03-05", "2026-03-01"), UserId));
        Assert.NotNull(await f.Service.Summarize(Request(tag, "2025-01-27", "2026-03-01"), UserId)); // exactly 400 days
    }

    [Fact]
    public async Task EmptyRange_HasNoFirstLastOrMaths_ButStillHasBuckets()
    {
        using var f = new Fixture(nameof(EmptyRange_HasNoFirstLastOrMaths_ButStillHasBuckets));
        var tag = f.Tag(InputTypeIds.Integer);

        var s = await f.Service.Summarize(Request(tag, "2026-03-01", "2026-03-02"), UserId);

        Assert.Equal(0, s.Count);
        Assert.Null(s.First);
        Assert.Null(s.Sum);
        Assert.Equal(0, s.CurrentStreak);
        Assert.Equal(2, s.Buckets.Count);
    }
}
