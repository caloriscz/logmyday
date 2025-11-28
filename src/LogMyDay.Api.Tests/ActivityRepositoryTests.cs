using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Tests for ActivityRepository focusing on performance-critical queries:
/// - GetRequiredDailyTagsNotFilledAsync (uses LEFT JOIN optimization)
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
    public async Task GetRequiredDailyTagsNotFilledAsync_ReturnsOnlyUnfilledTags()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new ActivityRepository(context);
        var userId = Guid.NewGuid();
        var today = DateTime.Today;

        // Create required daily tags
        var filledTag = new Tag 
        { 
            TagName = "FilledTag", 
            UserId = userId, 
            IsRequired = true, 
            TimeGranularity = TimeGranularity.Daily 
        };
        var unfilledTag = new Tag 
        { 
            TagName = "UnfilledTag", 
            UserId = userId, 
            IsRequired = true, 
            TimeGranularity = TimeGranularity.Daily 
        };
        var optionalTag = new Tag 
        { 
            TagName = "OptionalTag", 
            UserId = userId, 
            IsRequired = false, // Not required
            TimeGranularity = TimeGranularity.Daily 
        };

        context.Tags.AddRange(filledTag, unfilledTag, optionalTag);
        await context.SaveChangesAsync();

        // Add activity for filled tag (today)
        context.Activities.Add(new Activity 
        { 
            TagId = filledTag.Id, 
            UserId = userId, 
            DateStarted = today.AddHours(10), 
            Description = "Filled" 
        });
        await context.SaveChangesAsync();

        // Act - Get unfilled tags for today
        var unfilledTags = await repository.GetRequiredDailyTagsNotFilledAsync(today, userId);

        // Assert
        Assert.Single(unfilledTags);
        Assert.Equal("UnfilledTag", unfilledTags[0].TagName);
    }

    [Fact]
    public async Task GetRequiredDailyTagsNotFilledAsync_OnlyReturnsUserSpecificTags()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new ActivityRepository(context);
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var today = DateTime.Today;

        // Create required tags for different users
        var user1Tag = new Tag 
        { 
            TagName = "User1Tag", 
            UserId = user1Id, 
            IsRequired = true, 
            TimeGranularity = TimeGranularity.Daily 
        };
        var user2Tag = new Tag 
        { 
            TagName = "User2Tag", 
            UserId = user2Id, 
            IsRequired = true, 
            TimeGranularity = TimeGranularity.Daily 
        };

        context.Tags.AddRange(user1Tag, user2Tag);
        await context.SaveChangesAsync();

        // Act - Get unfilled tags for user1
        var user1UnfilledTags = await repository.GetRequiredDailyTagsNotFilledAsync(today, user1Id);

        // Assert - Should only return user1's tag
        Assert.Single(user1UnfilledTags);
        Assert.Equal("User1Tag", user1UnfilledTags[0].TagName);
        Assert.Equal(user1Id, user1UnfilledTags[0].UserId);
    }

    [Fact]
    public async Task GetRequiredDailyTagsNotFilledAsync_IgnoresNonDailyGranularity()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new ActivityRepository(context);
        var userId = Guid.NewGuid();
        var today = DateTime.Today;

        // Create tags with different time granularities
        var dailyTag = new Tag 
        { 
            TagName = "DailyTag", 
            UserId = userId, 
            IsRequired = true, 
            TimeGranularity = TimeGranularity.Daily 
        };
        var exactTimeTag = new Tag 
        { 
            TagName = "ExactTimeTag", 
            UserId = userId, 
            IsRequired = true, 
            TimeGranularity = TimeGranularity.Exact // Not daily
        };

        context.Tags.AddRange(dailyTag, exactTimeTag);
        await context.SaveChangesAsync();

        // Act
        var unfilledTags = await repository.GetRequiredDailyTagsNotFilledAsync(today, userId);

        // Assert - Only daily tag should be returned
        Assert.Single(unfilledTags);
        Assert.Equal("DailyTag", unfilledTags[0].TagName);
    }

    [Fact]
    public async Task GetRequiredDailyTagsNotFilledAsync_ConsidersDateBoundaries()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new ActivityRepository(context);
        var userId = Guid.NewGuid();
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);

        var tag = new Tag 
        { 
            TagName = "TestTag", 
            UserId = userId, 
            IsRequired = true, 
            TimeGranularity = TimeGranularity.Daily 
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        // Add activity for yesterday (not today)
        context.Activities.Add(new Activity 
        { 
            TagId = tag.Id, 
            UserId = userId, 
            DateStarted = yesterday.AddHours(15), 
            Description = "Yesterday" 
        });
        await context.SaveChangesAsync();

        // Act - Check today
        var unfilledTags = await repository.GetRequiredDailyTagsNotFilledAsync(today, userId);

        // Assert - Should show unfilled for today (yesterday's activity doesn't count)
        Assert.Single(unfilledTags);
        Assert.Equal("TestTag", unfilledTags[0].TagName);
    }

    [Fact]
    public async Task GetRequiredDailyTagsNotFilledAsync_IncludesRelatedEntities()
    {
        // Verify that the query includes InputType, Unit, and OptionList
        
        // Arrange
        using var context = CreateContext();
        var repository = new ActivityRepository(context);
        var userId = Guid.NewGuid();
        var today = DateTime.Today;

        var inputType = new InputType { Name = "TestInputType" };
        context.InputTypes.Add(inputType);
        await context.SaveChangesAsync();

        var tag = new Tag 
        { 
            TagName = "TestTag", 
            UserId = userId, 
            IsRequired = true, 
            TimeGranularity = TimeGranularity.Daily,
            InputTypeId = inputType.Id
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        // Act
        var unfilledTags = await repository.GetRequiredDailyTagsNotFilledAsync(today, userId);

        // Assert - Related entities should be included (not null)
        Assert.Single(unfilledTags);
        Assert.NotNull(unfilledTags[0].InputType);
        Assert.Equal("TestInputType", unfilledTags[0].InputType.Name);
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

