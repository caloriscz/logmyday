using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Controllers;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

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
                    UserId = Guid.Empty, // Different userId in backup
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
                    Description = "Test Activity",
                    UserId = Guid.Empty // Different userId in backup
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
                    UserId = user1Id, 
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
        Assert.Equal(user1Id, backup.Tags[0].UserId);
    }
}

public class BackupControllerTests
{
    private static readonly string CurrentBackupVersion = new BackupMetadata().Version;

    [Fact]
    public async Task GetBackupInfo_ShouldReturnOk_WithBackupMetadata()
    {
        // Arrange
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

        var controller = new BackupController(
            mockBackupService.Object,
            mockLogger.Object,
            mockAuthService.Object
        );

        // Act
        var result = await controller.GetBackupInfo();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify the service was called
        mockBackupService.Verify(s => s.ExportDataAsync(null), Times.Once);
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


