using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Tests;

public class ActivityServiceTests
{
    [Fact]
    public async Task Create_ShouldAddActivityAndReturnResponse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: "ActivityService_Create_Test")
            .Options;
        using var context = new LogMyDayDbContext(options);
        // Add a tag to reference (since Activity requires TagId)
        var tag = new Tag { TagName = "TestTag", InputTypeId = 1, IsRequired = false };
        context.Tags.Add(tag);
        context.SaveChanges();
        
        var repository = new ActivityRepository(context);
        var service = new ActivityService(context, repository);
        var request = new ActivityRequest
        {
            DateStarted = DateTime.UtcNow.AddHours(-1),
            DateFinished = DateTime.UtcNow,
            Description = "Test Activity",
            PrimaryTagId = tag.Id
        };

        var userId = Guid.NewGuid(); // Add a userId for the required parameter

        // Act
        var response = await service.Create(request, userId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(request.Description, response.Description);
        Assert.Equal(request.PrimaryTagId, response.PrimaryTagId);
        Assert.True(response.Id > 0);
        var activity = await context.Activities.FindAsync(response.Id);
        Assert.NotNull(activity);
        Assert.Equal(request.Description, activity.Description);
    }
}

