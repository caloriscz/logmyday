using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Controllers;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace LogMyDay.Api.Tests;

public class BackupServiceTests
{
    private static readonly string CurrentBackupVersion = new BackupMetadata().Version;

    private LogMyDayDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new LogMyDayDbContext(options);
    }

    [Fact]
    public async Task ExportDataAsync_ShouldReturnEmptyBackupData_WhenDatabaseIsEmpty()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);

        // Act
        var result = await service.ExportDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Metadata);
        Assert.Empty(result.Tags);
        Assert.Empty(result.Activities);
        Assert.Empty(result.InputTypes);
        Assert.Empty(result.Patterns);
        Assert.Equal(CurrentBackupVersion, result.Metadata.Version);
    }

    [Fact]
    public async Task ValidateBackupDataAsync_ShouldReturnValid_ForValidBackupData()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);

        var backupData = new BackupData
        {
            Metadata = new BackupMetadata { Version = CurrentBackupVersion },
            InputTypes = new List<InputTypeBackup> { new InputTypeBackup { Name = "Text" } },
            Patterns = new List<PatternBackup>
            {
                new PatternBackup
                {
                    Name = "Email",
                    PatternValue = ".*@.*",
                    Description = "Email pattern",
                },
            },
            Tags = new List<TagBackup>
            {
                new TagBackup
                {
                    TagName = "Work",
                    InputTypeName = "Text",
                    PatternName = "Email",
                },
            },
            Activities = new List<ActivityBackup>
            {
                new ActivityBackup
                {
                    TagName = "Work",
                    DateCreated = DateTime.UtcNow,
                    DateStarted = DateTime.UtcNow,
                },
            },
        };

        // Act
        var result = await service.ValidateBackupData(backupData);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateBackupDataAsync_ShouldReturnInvalid_ForDuplicateTagNames()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);

        var backupData = new BackupData
        {
            Tags = new List<TagBackup>
            {
                new TagBackup { TagName = "Work" },
                new TagBackup { TagName = "Work" }, // Duplicate
            },
            Activities = new List<ActivityBackup>(),
            InputTypes = new List<InputTypeBackup>(),
            Patterns = new List<PatternBackup>(),
        };

        // Act
        var result = await service.ValidateBackupData(backupData);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate tag names"));
    }

    [Fact]
    public async Task ClearDataAsync_ShouldReturnZero_WhenDatabaseIsEmpty()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);

        // Act
        var result = await service.ClearDataAsync();

        // Assert
        Assert.Equal(0, result);
    }

    // CRITICAL SECURITY TESTS - Prevent data loss and multi-user data leakage

    [Fact]
    public async Task ImportDataAsync_AssignsCorrectUserId_ToAllImportedEntities()
    {
        // This test validates the critical bug fix: imported entities MUST have userId assigned
        
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        var importingUserId = Guid.NewGuid();

        var backupData = new BackupData
        {
            Metadata = new BackupMetadata { Version = CurrentBackupVersion, ExportDate = DateTime.UtcNow },
            InputTypes = new List<InputTypeBackup>(),
            Patterns = new List<PatternBackup>(),
            Units = new List<UnitBackup>(),
            TagOptionLists = new List<TagOptionListBackup>(),
            TagOptions = new List<TagOptionBackup>(),
            Tags = new List<TagBackup>
            {
                new() 
                { 
                    TagName = "TestTag", 
                    IsRequired = true,
                    TimeGranularity = Domain.Enums.TimeGranularity.Daily
                }
            },
            Notifications = new List<NotificationBackup>(),
            Activities = new List<ActivityBackup>
            {
                new() 
                { 
                    TagName = "TestTag",
                    DateStarted = DateTime.Now,
                    Description = "Test Activity"
                }
            }
        };

        // Act - Import with specific userId
        var importResult = await service.ImportDataAsync(backupData, clearExistingData: false, userId: importingUserId);

        // Assert - Import should succeed
        Assert.True(importResult.Success, $"Import failed: {importResult.Message}. Errors: {string.Join(", ", importResult.Errors)}");

        // Assert - ALL imported entities should have the importing user's ID
        var importedTags = await context.Tags.ToListAsync();
        Assert.Single(importedTags);
        var importedTag = importedTags[0];
        Assert.Equal(importingUserId, importedTag.UserId);

        var importedActivities = await context.Activities.ToListAsync();
        Assert.Single(importedActivities);
        var importedActivity = importedActivities[0];
        Assert.Equal(importingUserId, importedActivity.UserId);
    }

    [Fact]
    public async Task ClearDataAsync_WithUserId_DeletesOnlyUserData()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        // Create data for user1
        var tag1 = new Domain.Entities.Tag 
        { 
            TagName = "User1Tag", 
            UserId = user1Id, 
            IsRequired = false, 
            TimeGranularity = Domain.Enums.TimeGranularity.Daily 
        };
        context.Tags.Add(tag1);
        await context.SaveChangesAsync();

        var activity1 = new Domain.Entities.Activity 
        { 
            TagId = tag1.Id, 
            UserId = user1Id, 
            DateStarted = DateTime.Now, 
            Description = "User1 Activity" 
        };
        context.Activities.Add(activity1);

        // Create data for user2
        var tag2 = new Domain.Entities.Tag 
        { 
            TagName = "User2Tag", 
            UserId = user2Id, 
            IsRequired = false, 
            TimeGranularity = Domain.Enums.TimeGranularity.Daily 
        };
        context.Tags.Add(tag2);
        await context.SaveChangesAsync();

        var activity2 = new Domain.Entities.Activity 
        { 
            TagId = tag2.Id, 
            UserId = user2Id, 
            DateStarted = DateTime.Now, 
            Description = "User2 Activity" 
        };
        context.Activities.Add(activity2);
        await context.SaveChangesAsync();

        // Act - Clear only user1's data
        var recordsCleared = await service.ClearDataAsync(userId: user1Id);

        // Assert
        Assert.True(recordsCleared > 0); // Should have deleted something

        // User1's data should be gone
        Assert.Empty(await context.Activities.Where(a => a.UserId == user1Id).ToListAsync());
        Assert.Empty(await context.Tags.Where(t => t.UserId == user1Id).ToListAsync());

        // User2's data should remain untouched
        Assert.Single(await context.Activities.Where(a => a.UserId == user2Id).ToListAsync());
        Assert.Single(await context.Tags.Where(t => t.UserId == user2Id).ToListAsync());
    }

    [Fact]
    public async Task ImportDataAsync_WithClearTrue_DoesNotAffectOtherUsers()
    {
        // CRITICAL: Verify that clear flag only affects the importing user
        
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        // Create data for both users
        var user1Tag = new Domain.Entities.Tag 
        { 
            TagName = "User1Tag", 
            UserId = user1Id, 
            IsRequired = false, 
            TimeGranularity = Domain.Enums.TimeGranularity.Daily 
        };
        var user2Tag = new Domain.Entities.Tag 
        { 
            TagName = "User2Tag", 
            UserId = user2Id, 
            IsRequired = false, 
            TimeGranularity = Domain.Enums.TimeGranularity.Daily 
        };
        context.Tags.AddRange(user1Tag, user2Tag);
        await context.SaveChangesAsync();

        // Backup data for user1
        var backupData = new BackupData
        {
            Metadata = new BackupMetadata { Version = CurrentBackupVersion, ExportDate = DateTime.UtcNow },
            InputTypes = new List<InputTypeBackup>(),
            Patterns = new List<PatternBackup>(),
            Units = new List<UnitBackup>(),
            TagOptionLists = new List<TagOptionListBackup>(),
            TagOptions = new List<TagOptionBackup>(),
            Tags = new List<TagBackup>
            {
                new() 
                { 
                    TagName = "User1NewTag", 
                    IsRequired = false, 
                    TimeGranularity = Domain.Enums.TimeGranularity.Daily 
                }
            },
            Notifications = new List<NotificationBackup>(),
            Activities = new List<ActivityBackup>()
        };

        // Act - User1 imports with clear flag
        await service.ImportDataAsync(backupData, clearExistingData: true, userId: user1Id);

        // Assert - User2's data should be completely untouched
        var user2Tags = await context.Tags.Where(t => t.UserId == user2Id).ToListAsync();
        Assert.Single(user2Tags);
        Assert.Equal("User2Tag", user2Tags[0].TagName);
    }

    [Fact]
    public async Task ExportDataAsync_WithUserId_ExportsOnlyUserData()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        // Create tags for both users
        context.Tags.Add(new Domain.Entities.Tag 
        { 
            TagName = "User1Tag", 
            UserId = user1Id, 
            IsRequired = false, 
            TimeGranularity = Domain.Enums.TimeGranularity.Daily 
        });
        context.Tags.Add(new Domain.Entities.Tag 
        { 
            TagName = "User2Tag", 
            UserId = user2Id, 
            IsRequired = false, 
            TimeGranularity = Domain.Enums.TimeGranularity.Daily 
        });
        await context.SaveChangesAsync();

        // Act - Export only user1's data
        var backup = await service.ExportDataAsync(userId: user1Id);

        // Assert
        Assert.Single(backup.Tags);
        Assert.Equal("User1Tag", backup.Tags[0].TagName);
    }

    // Streak compression tests

    [Fact]
    public async Task ExportDataAsync_CompressesConsecutiveDaysIntoStreak()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        var userId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1);

        var tag = new Domain.Entities.Tag
        {
            TagName = "Supplement",
            UserId = userId,
            IsRequired = false,
            TimeGranularity = Domain.Enums.TimeGranularity.Daily
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        // Add 5 consecutive days with same description
        for (int i = 0; i < 5; i++)
        {
            context.Activities.Add(new Domain.Entities.Activity
            {
                TagId = tag.Id,
                UserId = userId,
                DateStarted = baseDate.AddDays(i),
                Description = "1 pill"
            });
        }
        await context.SaveChangesAsync();

        // Act
        var backup = await service.ExportDataAsync(userId: userId);

        // Assert - 5 consecutive days should compress into 1 streak record
        Assert.Single(backup.Activities);
        var streak = backup.Activities[0];
        Assert.Equal("Supplement", streak.TagName);
        Assert.Equal("1 pill", streak.Description);
        Assert.Equal(baseDate.Date, streak.DateStarted.Date);
        Assert.NotNull(streak.StreakEndDate);
        Assert.Equal(baseDate.AddDays(4).Date, streak.StreakEndDate!.Value.Date);
        // TotalActivities in metadata should reflect expanded count
        Assert.Equal(5, backup.Metadata.TotalActivities);
    }

    [Fact]
    public async Task ExportDataAsync_DoesNotMergeGaps()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        var userId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1);

        var tag = new Domain.Entities.Tag
        {
            TagName = "Exercise",
            UserId = userId,
            IsRequired = false,
            TimeGranularity = Domain.Enums.TimeGranularity.Daily
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        // Add days 1,2,3 then skip day 4, add day 5,6
        int[] days = { 0, 1, 2, 4, 5 };
        foreach (var d in days)
        {
            context.Activities.Add(new Domain.Entities.Activity
            {
                TagId = tag.Id,
                UserId = userId,
                DateStarted = baseDate.AddDays(d),
                Description = "Run"
            });
        }
        await context.SaveChangesAsync();

        // Act
        var backup = await service.ExportDataAsync(userId: userId);

        // Assert - gap splits into 2 streaks: days 0-2 and days 4-5
        Assert.Equal(2, backup.Activities.Count);
        Assert.Equal(baseDate.AddDays(2).Date, backup.Activities[0].StreakEndDate!.Value.Date);
        Assert.Equal(baseDate.AddDays(5).Date, backup.Activities[1].StreakEndDate!.Value.Date);
        Assert.Equal(5, backup.Metadata.TotalActivities);
    }

    [Fact]
    public async Task ExportDataAsync_DoesNotMergeDifferentDescriptions()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        var userId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1);

        var tag = new Domain.Entities.Tag
        {
            TagName = "Supplement",
            UserId = userId,
            IsRequired = false,
            TimeGranularity = Domain.Enums.TimeGranularity.Daily
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        // Day 1-2: "1 pill", Day 3-4: "2 pills"
        context.Activities.Add(new Domain.Entities.Activity { TagId = tag.Id, UserId = userId, DateStarted = baseDate, Description = "1 pill" });
        context.Activities.Add(new Domain.Entities.Activity { TagId = tag.Id, UserId = userId, DateStarted = baseDate.AddDays(1), Description = "1 pill" });
        context.Activities.Add(new Domain.Entities.Activity { TagId = tag.Id, UserId = userId, DateStarted = baseDate.AddDays(2), Description = "2 pills" });
        context.Activities.Add(new Domain.Entities.Activity { TagId = tag.Id, UserId = userId, DateStarted = baseDate.AddDays(3), Description = "2 pills" });
        await context.SaveChangesAsync();

        // Act
        var backup = await service.ExportDataAsync(userId: userId);

        // Assert - different descriptions split into 2 streaks
        Assert.Equal(2, backup.Activities.Count);
        Assert.Equal("1 pill", backup.Activities[0].Description);
        Assert.Equal("2 pills", backup.Activities[1].Description);
    }

    [Fact]
    public async Task ExportDataAsync_SingleActivity_NoStreak()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        var userId = Guid.NewGuid();

        var tag = new Domain.Entities.Tag
        {
            TagName = "OneOff",
            UserId = userId,
            IsRequired = false,
            TimeGranularity = Domain.Enums.TimeGranularity.Daily
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        context.Activities.Add(new Domain.Entities.Activity
        {
            TagId = tag.Id,
            UserId = userId,
            DateStarted = new DateTime(2026, 3, 15),
            Description = "Single event"
        });
        await context.SaveChangesAsync();

        // Act
        var backup = await service.ExportDataAsync(userId: userId);

        // Assert - single activity should NOT have StreakEndDate
        Assert.Single(backup.Activities);
        Assert.Null(backup.Activities[0].StreakEndDate);
        Assert.Equal(1, backup.Metadata.TotalActivities);
    }

    [Fact]
    public async Task ImportDataAsync_ExpandsStreaksToIndividualActivities()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        var userId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 2, 1);

        var backupData = new BackupData
        {
            Metadata = new BackupMetadata { Version = CurrentBackupVersion, ExportDate = DateTime.UtcNow },
            InputTypes = new List<InputTypeBackup>(),
            Patterns = new List<PatternBackup>(),
            Units = new List<UnitBackup>(),
            TagOptionLists = new List<TagOptionListBackup>(),
            TagOptions = new List<TagOptionBackup>(),
            TagGroups = new List<TagGroupBackup>(),
            Tags = new List<TagBackup>
            {
                new() { TagName = "Vitamin", IsRequired = false, TimeGranularity = Domain.Enums.TimeGranularity.Daily }
            },
            Notifications = new List<NotificationBackup>(),
            Activities = new List<ActivityBackup>
            {
                new()
                {
                    TagName = "Vitamin",
                    DateCreated = DateTime.UtcNow,
                    DateStarted = baseDate,
                    StreakEndDate = baseDate.AddDays(4), // 5 days: Feb 1-5
                    Description = "1 tablet"
                }
            },
            ScanMappings = new List<ScanMappingBackup>()
        };

        // Act
        var result = await service.ImportDataAsync(backupData, clearExistingData: false, userId: userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, result.Statistics.ActivitiesImported);

        var dbActivities = await context.Activities
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.DateStarted)
            .ToListAsync();

        Assert.Equal(5, dbActivities.Count);
        Assert.Equal(baseDate.Date, dbActivities[0].DateStarted.Date);
        Assert.Equal(baseDate.AddDays(4).Date, dbActivities[4].DateStarted.Date);
        Assert.All(dbActivities, a => Assert.Equal("1 tablet", a.Description));
    }

    [Fact]
    public async Task ImportDataAsync_HandlesLegacyFormatWithoutStreakEndDate()
    {
        // Arrange — simulates a v1.6 backup with no StreakEndDate
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);
        var userId = Guid.NewGuid();

        var backupData = new BackupData
        {
            Metadata = new BackupMetadata { Version = CurrentBackupVersion, ExportDate = DateTime.UtcNow },
            InputTypes = new List<InputTypeBackup>(),
            Patterns = new List<PatternBackup>(),
            Units = new List<UnitBackup>(),
            TagOptionLists = new List<TagOptionListBackup>(),
            TagOptions = new List<TagOptionBackup>(),
            TagGroups = new List<TagGroupBackup>(),
            Tags = new List<TagBackup>
            {
                new() { TagName = "LegacyTag", IsRequired = false, TimeGranularity = Domain.Enums.TimeGranularity.Daily }
            },
            Notifications = new List<NotificationBackup>(),
            Activities = new List<ActivityBackup>
            {
                new()
                {
                    TagName = "LegacyTag",
                    DateCreated = DateTime.UtcNow,
                    DateStarted = new DateTime(2026, 1, 15),
                    Description = "Legacy activity"
                    // StreakEndDate is null — legacy format
                }
            },
            ScanMappings = new List<ScanMappingBackup>()
        };

        // Act
        var result = await service.ImportDataAsync(backupData, clearExistingData: false, userId: userId);

        // Assert — single activity imported, no expansion
        Assert.True(result.Success);
        Assert.Equal(1, result.Statistics.ActivitiesImported);
        var dbActivities = await context.Activities.Where(a => a.UserId == userId).ToListAsync();
        Assert.Single(dbActivities);
    }

    [Fact]
    public async Task ExportImportRoundTrip_PreservesAllActivities()
    {
        // Arrange — create activities, export (with compression), import into fresh DB
        using var exportContext = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var exportService = new BackupService(exportContext, logger.Object);
        var userId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 1, 1);

        var tag = new Domain.Entities.Tag
        {
            TagName = "RoundTrip",
            UserId = userId,
            IsRequired = false,
            TimeGranularity = Domain.Enums.TimeGranularity.Daily
        };
        exportContext.Tags.Add(tag);
        await exportContext.SaveChangesAsync();

        // 3 consecutive + 1 standalone
        for (int i = 0; i < 3; i++)
        {
            exportContext.Activities.Add(new Domain.Entities.Activity
            {
                TagId = tag.Id,
                UserId = userId,
                DateStarted = baseDate.AddDays(i),
                Description = "Daily"
            });
        }
        exportContext.Activities.Add(new Domain.Entities.Activity
        {
            TagId = tag.Id,
            UserId = userId,
            DateStarted = baseDate.AddDays(10),
            Description = "One-off"
        });
        await exportContext.SaveChangesAsync();

        // Act — export
        var backup = await exportService.ExportDataAsync(userId: userId);
        Assert.Equal(4, backup.Metadata.TotalActivities);

        // Import into fresh context
        using var importContext = CreateContext();
        var importService = new BackupService(importContext, new Mock<ILogger<BackupService>>().Object);
        var importResult = await importService.ImportDataAsync(backup, clearExistingData: false, userId: userId);

        // Assert — all 4 activities restored
        Assert.True(importResult.Success);
        Assert.Equal(4, importResult.Statistics.ActivitiesImported);

        var importedActivities = await importContext.Activities
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.DateStarted)
            .ToListAsync();

        Assert.Equal(4, importedActivities.Count);
        Assert.Equal(baseDate.Date, importedActivities[0].DateStarted.Date);
        Assert.Equal(baseDate.AddDays(1).Date, importedActivities[1].DateStarted.Date);
        Assert.Equal(baseDate.AddDays(2).Date, importedActivities[2].DateStarted.Date);
        Assert.Equal(baseDate.AddDays(10).Date, importedActivities[3].DateStarted.Date);
    }

    [Fact]
    public async Task ValidateBackupData_RejectsInvalidStreakEndDate()
    {
        // Arrange
        using var context = CreateContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);

        var backupData = new BackupData
        {
            Tags = new List<TagBackup>
            {
                new TagBackup { TagName = "Test" }
            },
            Activities = new List<ActivityBackup>
            {
                new ActivityBackup
                {
                    TagName = "Test",
                    DateCreated = DateTime.UtcNow,
                    DateStarted = new DateTime(2026, 3, 10),
                    StreakEndDate = new DateTime(2026, 3, 5), // Before DateStarted — invalid
                    Description = "Bad streak"
                }
            },
            InputTypes = new List<InputTypeBackup>(),
            Patterns = new List<PatternBackup>(),
        };

        // Act
        var result = await service.ValidateBackupData(backupData);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("StreakEndDate before DateStarted"));
    }
}

public class BackupControllerTests
{
    private static readonly string CurrentBackupVersion = new BackupMetadata().Version;

    [Fact]
    public async Task GetBackupInfo_ShouldReturnOk_ScopedToAuthenticatedUser()
    {
        // Arrange
        var authenticatedUserId = Guid.NewGuid();
        var mockBackupService = new Mock<IBackupService>();
        var mockLogger = new Mock<ILogger<BackupController>>();
        var mockAuthService = new Mock<IAuthService>();

        var backupData = new BackupData
        {
            Metadata = new BackupMetadata
            {
                ExportDate = DateTime.UtcNow,
                Version = CurrentBackupVersion,
                TotalTags = 5,
                TotalActivities = 10,
            },
        };

        mockBackupService.Setup(s => s.ExportDataAsync(It.IsAny<Guid?>())).ReturnsAsync(backupData);
        mockAuthService.Setup(a => a.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(authenticatedUserId);

        var controller = new BackupController(
            mockBackupService.Object,
            mockLogger.Object,
            mockAuthService.Object
        )
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.GetBackupInfo();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Statistics are always scoped to the authenticated user — never the all-users aggregate (null)
        mockBackupService.Verify(s => s.ExportDataAsync(authenticatedUserId), Times.Once);
        mockBackupService.Verify(s => s.ExportDataAsync(null), Times.Never);
    }

    [Fact]
    public async Task GetBackupInfo_ShouldScopeToCaller_NotAnotherUser()
    {
        // Regression: a caller must never receive another user's (or cross-user aggregate) statistics.
        // The endpoint no longer accepts a client userId, so the only id reaching the service is the
        // authenticated caller's, regardless of who the "victim" would be.
        var callerId = Guid.NewGuid();
        var victimId = Guid.NewGuid();

        var mockBackupService = new Mock<IBackupService>();
        var mockLogger = new Mock<ILogger<BackupController>>();
        var mockAuthService = new Mock<IAuthService>();

        mockBackupService
            .Setup(s => s.ExportDataAsync(It.IsAny<Guid?>()))
            .ReturnsAsync(new BackupData { Metadata = new BackupMetadata { Version = CurrentBackupVersion } });
        mockAuthService.Setup(a => a.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(callerId);

        var controller = new BackupController(
            mockBackupService.Object,
            mockLogger.Object,
            mockAuthService.Object
        )
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        await controller.GetBackupInfo();

        // Assert — only the caller's data is ever requested
        mockBackupService.Verify(s => s.ExportDataAsync(callerId), Times.Once);
        mockBackupService.Verify(s => s.ExportDataAsync(victimId), Times.Never);
        mockBackupService.Verify(s => s.ExportDataAsync(null), Times.Never);
    }

    [Fact]
    public async Task GetBackupInfo_ShouldReturnUnauthorized_WhenUserIdMissing()
    {
        // Arrange
        var mockBackupService = new Mock<IBackupService>();
        var mockLogger = new Mock<ILogger<BackupController>>();
        var mockAuthService = new Mock<IAuthService>();

        mockAuthService.Setup(a => a.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns((Guid?)null);

        var controller = new BackupController(
            mockBackupService.Object,
            mockLogger.Object,
            mockAuthService.Object
        )
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.GetBackupInfo();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        mockBackupService.Verify(s => s.ExportDataAsync(It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task ClearData_ShouldReturnOk_WithRecordsCleared()
    {
        // Arrange
        var mockBackupService = new Mock<IBackupService>();
        var mockLogger = new Mock<ILogger<BackupController>>();
        var mockAuthService = new Mock<IAuthService>();

        mockBackupService.Setup(s => s.ClearDataAsync(It.IsAny<Guid?>())).ReturnsAsync(42);

        var controller = new BackupController(
            mockBackupService.Object,
            mockLogger.Object,
            mockAuthService.Object
        );

        // Act
        var result = await controller.ClearData();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify the service was called
        mockBackupService.Verify(s => s.ClearDataAsync(null), Times.Once);
    }
}


