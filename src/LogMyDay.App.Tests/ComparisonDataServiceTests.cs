using LogMyDay.App.Services.Insights;
using LogMyDay.Domain.Constants;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Moq;

namespace LogMyDay.App.Tests;

public class ComparisonDataServiceTests
{
    private const int CoffeeTagId = 1;
    private const int SleepTagId = 2;

    private static readonly DateTime RangeStart = new(2026, 3, 13);
    private static readonly DateTime RangeEnd = new(2026, 3, 26);

    private static ActivityResponse Activity(DateTime dateStarted, string? description, int tagId = CoffeeTagId)
    {
        return new ActivityResponse
        {
            Id = 0,
            DateStarted = dateStarted,
            Description = description,
            PrimaryTagId = tagId
        };
    }

    private static Mock<IActivityApi> ApiReturning(params ActivityResponse[] items)
    {
        var api = new Mock<IActivityApi>();

        api.Setup(a => a.GetActivities(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
            .ReturnsAsync(new PagedResult<ActivityResponse>
            {
                Items = items.ToList(),
                TotalCount = items.Length,
                PageNumber = 1,
                PageSize = ChartConstants.DefaultPageSize
            });

        return api;
    }

    // --- Fetch behaviour ---

    [Fact]
    public async Task LoadDailyValues_SameTagInMultipleRows_FetchesTagOnce()
    {
        // The year-over-year case puts one tag in two rows; fetching twice would double the API traffic.
        var api = ApiReturning();
        var service = new ComparisonDataService(api.Object);

        await service.LoadDailyValues([CoffeeTagId, CoffeeTagId, SleepTagId], RangeStart, RangeEnd);

        api.Verify(
            a => a.GetActivities(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), CoffeeTagId,
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()),
            Times.Once);
        api.Verify(
            a => a.GetActivities(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), SleepTagId,
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadDailyValues_NoTags_ReturnsEmptyAndNeverCallsApi()
    {
        var api = ApiReturning();
        var service = new ComparisonDataService(api.Object);

        var data = await service.LoadDailyValues([], RangeStart, RangeEnd);

        Assert.Empty(data.ValuesByTag);
        Assert.False(data.IsTruncated);
        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoadDailyValues_PassesStartDateAsGivenRangeStart()
    {
        var api = ApiReturning();
        var service = new ComparisonDataService(api.Object);

        await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        api.Verify(
            a => a.GetActivities(1, ChartConstants.DefaultPageSize, "asc", CoffeeTagId,
                RangeStart, It.IsAny<DateTime?>(), null),
            Times.Once);
    }

    [Fact]
    public async Task LoadDailyValues_PassesEndDateAtEndOfLastDay()
    {
        // ActivityService filters DateStarted <= endDate, so a midnight end would drop the final day.
        var api = ApiReturning();
        var service = new ComparisonDataService(api.Object);
        DateTime? captured = null;

        api.Setup(a => a.GetActivities(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
            .Callback((int _, int _, string _, int? _, DateTime? _, DateTime? end, string? _) => captured = end)
            .ReturnsAsync(new PagedResult<ActivityResponse> { Items = [] });

        await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        Assert.Equal(RangeEnd.Date.AddDays(1).AddTicks(-1), captured);
        Assert.Equal(new TimeSpan(0, 23, 59, 59, 999).Add(TimeSpan.FromTicks(9999)), captured!.Value.TimeOfDay);
    }

    [Fact]
    public async Task LoadDailyValues_RangeEndWithTimeComponent_StillEndsAtEndOfThatDay()
    {
        var api = ApiReturning();
        var service = new ComparisonDataService(api.Object);

        await service.LoadDailyValues([CoffeeTagId], RangeStart, new DateTime(2026, 3, 26, 9, 30, 0));

        api.Verify(
            a => a.GetActivities(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<DateTime?>(), RangeEnd.AddDays(1).AddTicks(-1), It.IsAny<string?>()),
            Times.Once);
    }

    // --- Bucketing ---

    [Fact]
    public async Task LoadDailyValues_GroupsActivitiesByCalendarDay()
    {
        var api = ApiReturning(
            Activity(new DateTime(2026, 3, 20), "1"),
            Activity(new DateTime(2026, 3, 20), "2"),
            Activity(new DateTime(2026, 3, 21), "3"));
        var service = new ComparisonDataService(api.Object);

        var data = await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        Assert.Equal(["1", "2"], data.GetValues(CoffeeTagId, new DateTime(2026, 3, 20)));
        Assert.Equal(["3"], data.GetValues(CoffeeTagId, new DateTime(2026, 3, 21)));
    }

    [Fact]
    public async Task LoadDailyValues_ActivitiesWithTimeComponents_LandInSameDayBucket()
    {
        var api = ApiReturning(
            Activity(new DateTime(2026, 3, 20, 7, 15, 0), "morning"),
            Activity(new DateTime(2026, 3, 20, 22, 45, 0), "evening"));
        var service = new ComparisonDataService(api.Object);

        var data = await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        Assert.Equal(2, data.GetValues(CoffeeTagId, new DateTime(2026, 3, 20)).Count);
    }

    [Fact]
    public async Task LoadDailyValues_PreservesChronologicalOrderWithinDay()
    {
        // First aggregation reads index 0, so the ascending order the API returns must survive bucketing.
        var api = ApiReturning(
            Activity(new DateTime(2026, 3, 20, 6, 0, 0), "first"),
            Activity(new DateTime(2026, 3, 20, 12, 0, 0), "second"),
            Activity(new DateTime(2026, 3, 20, 18, 0, 0), "third"));
        var service = new ComparisonDataService(api.Object);

        var data = await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        Assert.Equal(["first", "second", "third"], data.GetValues(CoffeeTagId, new DateTime(2026, 3, 20)));
    }

    [Fact]
    public async Task LoadDailyValues_NullDescription_StoredAsEmptyString()
    {
        var api = ApiReturning(Activity(new DateTime(2026, 3, 20), null));
        var service = new ComparisonDataService(api.Object);

        var data = await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        Assert.Equal([string.Empty], data.GetValues(CoffeeTagId, new DateTime(2026, 3, 20)));
    }

    [Fact]
    public async Task LoadDailyValues_RequestsAscendingOrder()
    {
        var api = ApiReturning();
        var service = new ComparisonDataService(api.Object);

        await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        api.Verify(
            a => a.GetActivities(It.IsAny<int>(), It.IsAny<int>(), "asc", It.IsAny<int?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()),
            Times.Once);
    }

    // --- Truncation and cancellation ---

    [Fact]
    public async Task LoadDailyValues_ItemCountAtPageSize_SetsIsTruncated()
    {
        var items = Enumerable
            .Range(0, ChartConstants.DefaultPageSize)
            .Select(i => Activity(new DateTime(2026, 3, 20), i.ToString()))
            .ToArray();
        var service = new ComparisonDataService(ApiReturning(items).Object);

        var data = await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        Assert.True(data.IsTruncated);
    }

    [Fact]
    public async Task LoadDailyValues_ItemCountBelowPageSize_IsNotTruncated()
    {
        var service = new ComparisonDataService(ApiReturning(Activity(new DateTime(2026, 3, 20), "1")).Object);

        var data = await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        Assert.False(data.IsTruncated);
    }

    [Fact]
    public async Task LoadDailyValues_CancelledToken_Throws()
    {
        var service = new ComparisonDataService(ApiReturning().Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd, cts.Token));
    }

    // --- ComparisonDataSet lookups ---

    [Fact]
    public async Task GetValues_UnknownTagOrDate_ReturnsEmptyList()
    {
        var api = ApiReturning(Activity(new DateTime(2026, 3, 20), "1"));
        var service = new ComparisonDataService(api.Object);

        var data = await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        Assert.Empty(data.GetValues(999, new DateTime(2026, 3, 20)));
        Assert.Empty(data.GetValues(CoffeeTagId, new DateTime(2026, 3, 19)));
    }

    [Fact]
    public async Task GetValues_DateWithTimeComponent_StillFindsTheDay()
    {
        var api = ApiReturning(Activity(new DateTime(2026, 3, 20), "1"));
        var service = new ComparisonDataService(api.Object);

        var data = await service.LoadDailyValues([CoffeeTagId], RangeStart, RangeEnd);

        Assert.Equal(["1"], data.GetValues(CoffeeTagId, new DateTime(2026, 3, 20, 15, 0, 0)));
    }
}
