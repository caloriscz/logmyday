using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;
using ClosedXML.Excel;

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

    [Fact]
    public async Task GenerateExcelReport_WithNullDates_IncludesAllActivities()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExcelExportService>();
        var context = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Infrastructure.Data.LogMyDayDbContext>();
        
        var testUserId = context.Users.First().Id;
        var tagIds = context.Tags.Select(t => t.Id).ToList();

        var request = new ExcelExportRequest
        {
            TagIds = tagIds,
            StartDate = null,
            EndDate = null,
            UserId = testUserId,
            FreezeFirstRow = true
        };

        var result = await service.GenerateExcelReport(request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.FileContent);
        Assert.True(result.FileContent.Length > 0);
        Assert.Equal(5, result.Statistics.TotalActivities);
    }

    [Fact]
    public async Task GenerateExcelReport_WithDateRange_FiltersActivities()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExcelExportService>();
        var context = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Infrastructure.Data.LogMyDayDbContext>();
        
        var testUserId = context.Users.First().Id;
        var tagIds = context.Tags.Select(t => t.Id).ToList();

        var request = new ExcelExportRequest
        {
            TagIds = tagIds,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31),
            UserId = testUserId,
            FreezeFirstRow = false
        };

        var result = await service.GenerateExcelReport(request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.FileContent);
        Assert.True(result.FileContent.Length > 0);
        Assert.Equal(2, result.Statistics.TotalActivities);
    }

    [Fact]
    public async Task GenerateExcelReport_NumericValues_StoredAsNumbers()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IExcelExportService>();
        var context = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Infrastructure.Data.LogMyDayDbContext>();
        
        var testUserId = context.Users.First().Id;
        var tagIds = context.Tags.Select(t => t.Id).ToList();

        var request = new ExcelExportRequest
        {
            TagIds = tagIds,
            StartDate = null,
            EndDate = null,
            UserId = testUserId,
            FreezeFirstRow = true
        };

        var result = await service.GenerateExcelReport(request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.FileContent);

        // Load the Excel file and verify numeric cells are stored as numbers, not text
        using var memoryStream = new MemoryStream(result.FileContent);
        using var workbook = new XLWorkbook(memoryStream);
        var worksheet = workbook.Worksheet(1);

        // Find cells with numeric values and verify they don't have text prefix
        for (int row = 2; row <= worksheet.LastRowUsed().RowNumber(); row++)
        {
            for (int col = 2; col <= worksheet.LastColumnUsed().ColumnNumber(); col++)
            {
                var cell = worksheet.Cell(row, col);
                var cellValue = cell.Value;

                // If the cell contains a numeric value
                if (cellValue.IsNumber)
                {
                    // Verify it's stored as a number, not as text
                    // If it were text with apostrophe prefix, IsNumber would be false
                    Assert.True(cell.DataType == XLDataType.Number, 
                        $"Cell {cell.Address} contains numeric value but is stored as {cell.DataType} instead of Number");
                }
            }
        }
    }
}
