using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Tests for ActivityRepository focusing on performance-critical queries:
/// - GetAvailableYearsAsync (year filtering logic)
/// </summary>
public class ActivityRepositoryTests
{
    private static LogMyDayDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new LogMyDayDbContext(options);
    }

    [Fact]
    public async Task GetAvailableYearsAsync_ReturnsDistinctYears()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new ActivityRepository(context);
        var userId = Guid.NewGuid();

        var tag = new Tag 
        { 
            TagName = "TestTag", 
            UserId = userId, 
            IsRequired = false, 
            TimeGranularity = TimeGranularity.Daily 
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        // Add activities in different years
        context.Activities.AddRange(
            new Activity { TagId = tag.Id, UserId = userId, DateStarted = new DateTime(2023, 1, 1), Description = "2023" },
            new Activity { TagId = tag.Id, UserId = userId, DateStarted = new DateTime(2023, 6, 15), Description = "2023 again" },
            new Activity { TagId = tag.Id, UserId = userId, DateStarted = new DateTime(2024, 3, 10), Description = "2024" },
            new Activity { TagId = tag.Id, UserId = userId, DateStarted = new DateTime(2025, 11, 12), Description = "2025" }
        );
        await context.SaveChangesAsync();

        // Act
        var years = await repository.GetAvailableYearsAsync(userId);

        // Assert
        Assert.Equal(3, years.Count); // 2023, 2024, 2025
        Assert.Contains(2023, years);
        Assert.Contains(2024, years);
        Assert.Contains(2025, years);
    }

    [Fact]
    public async Task GetAvailableYearsAsync_OrdersDescending()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new ActivityRepository(context);
        var userId = Guid.NewGuid();

        var tag = new Tag 
        { 
            TagName = "TestTag", 
            UserId = userId, 
            IsRequired = false, 
            TimeGranularity = TimeGranularity.Daily 
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        context.Activities.AddRange(
            new Activity { TagId = tag.Id, UserId = userId, DateStarted = new DateTime(2020, 1, 1), Description = "Oldest" },
            new Activity { TagId = tag.Id, UserId = userId, DateStarted = new DateTime(2025, 1, 1), Description = "Newest" }
        );
        await context.SaveChangesAsync();

        // Act
        var years = await repository.GetAvailableYearsAsync(userId);

        // Assert - Should be descending (newest first)
        Assert.Equal(2, years.Count);
        Assert.Equal(2025, years[0]);
        Assert.Equal(2020, years[1]);
    }

    [Fact]
    public async Task GetAvailableYearsAsync_WithTagFilter_OnlyIncludesSpecificTag()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new ActivityRepository(context);
        var userId = Guid.NewGuid();

        var tag1 = new Tag { TagName = "Tag1", UserId = userId, IsRequired = false, TimeGranularity = TimeGranularity.Daily };
        var tag2 = new Tag { TagName = "Tag2", UserId = userId, IsRequired = false, TimeGranularity = TimeGranularity.Daily };
        context.Tags.AddRange(tag1, tag2);
        await context.SaveChangesAsync();

        context.Activities.AddRange(
            new Activity { TagId = tag1.Id, UserId = userId, DateStarted = new DateTime(2023, 1, 1), Description = "Tag1 2023" },
            new Activity { TagId = tag1.Id, UserId = userId, DateStarted = new DateTime(2024, 1, 1), Description = "Tag1 2024" },
            new Activity { TagId = tag2.Id, UserId = userId, DateStarted = new DateTime(2025, 1, 1), Description = "Tag2 2025" }
        );
        await context.SaveChangesAsync();

        // Act - Get years for tag1 only
        var years = await repository.GetAvailableYearsAsync(userId, tagId: tag1.Id);

        // Assert - Should only include years from tag1 activities
        Assert.Equal(2, years.Count);
        Assert.Contains(2023, years);
        Assert.Contains(2024, years);
        Assert.DoesNotContain(2025, years);
    }

    [Fact]
    public async Task GetAvailableYearsAsync_OnlyReturnsUserData()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new ActivityRepository(context);
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var tag1 = new Tag { TagName = "User1Tag", UserId = user1Id, IsRequired = false, TimeGranularity = TimeGranularity.Daily };
        var tag2 = new Tag { TagName = "User2Tag", UserId = user2Id, IsRequired = false, TimeGranularity = TimeGranularity.Daily };
        context.Tags.AddRange(tag1, tag2);
        await context.SaveChangesAsync();

        context.Activities.AddRange(
            new Activity { TagId = tag1.Id, UserId = user1Id, DateStarted = new DateTime(2023, 1, 1), Description = "User1" },
            new Activity { TagId = tag2.Id, UserId = user2Id, DateStarted = new DateTime(2024, 1, 1), Description = "User2" }
        );
        await context.SaveChangesAsync();

        // Act
        var user1Years = await repository.GetAvailableYearsAsync(user1Id);

        // Assert - Should only see user1's years
        Assert.Single(user1Years);
        Assert.Contains(2023, user1Years);
        Assert.DoesNotContain(2024, user1Years);
    }
}

