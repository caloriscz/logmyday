using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace LogMyDay.Api.IntegrationTests;

public class ExportServiceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ExportServiceIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetActivitiesForExport_WithNullDates_ReturnsAllActivities()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExcelExportService>();
        var context = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Infrastructure.Data.LogMyDayDbContext>();
        
        var testUserId = context.Users.First().Id;

        var request = new ExcelExportRequest
        {
            TagIds = context.Tags.Select(t => t.Id).ToList(),
            StartDate = null,
            EndDate = null,
            UserId = testUserId
        };

        var activities = await service.GetActivitiesForExport(request);

        Assert.NotNull(activities);
        Assert.Equal(5, activities.Count);
    }

    [Fact]
    public async Task GetActivitiesForExport_WithDateRange_FiltersCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExcelExportService>();
        var context = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Infrastructure.Data.LogMyDayDbContext>();
        
        var testUserId = context.Users.First().Id;

        var request = new ExcelExportRequest
        {
            TagIds = context.Tags.Select(t => t.Id).ToList(),
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31),
            UserId = testUserId
        };

        var activities = await service.GetActivitiesForExport(request);

        Assert.NotNull(activities);
        Assert.Equal(2, activities.Count);
    }

    [Fact]
    public async Task GetActivitiesForExport_IncludesTimeGranularity()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExcelExportService>();
        var context = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Infrastructure.Data.LogMyDayDbContext>();
        
        var testUserId = context.Users.First().Id;

        var request = new ExcelExportRequest
        {
            TagIds = context.Tags.Select(t => t.Id).ToList(),
            StartDate = null,
            EndDate = null,
            UserId = testUserId
        };

        var activities = await service.GetActivitiesForExport(request);

        Assert.NotNull(activities);
        
        var exerciseActivity = activities.First(a => a.Tag == "Exercise");
        Assert.Equal(1, exerciseActivity.TimeGranularity);
        
        var sleepActivity = activities.First(a => a.Tag == "Sleep");
        Assert.Equal(0, sleepActivity.TimeGranularity);
        
        var moodActivity = activities.First(a => a.Tag == "Mood");
        Assert.Equal(2, moodActivity.TimeGranularity);
    }

    [Fact]
    public async Task GetActivitiesForExport_WithNoMatchingTags_ReturnsEmptyList()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExcelExportService>();
        var context = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Infrastructure.Data.LogMyDayDbContext>();
        
        var testUserId = context.Users.First().Id;

        var request = new ExcelExportRequest
        {
            TagIds = new List<int> { 999 },
            StartDate = null,
            EndDate = null,
            UserId = testUserId
        };

        var activities = await service.GetActivitiesForExport(request);

        Assert.NotNull(activities);
        Assert.Empty(activities);
    }
}
