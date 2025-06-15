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
    private LogMyDayDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new LogMyDayDbContext(options);
    }

    [Fact]
    public async Task ExportDataAsync_ShouldReturnEmptyBackupData_WhenDatabaseIsEmpty()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);

        // Act
        var result = await service.ExportDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Metadata);
        Assert.Equal(0, result.Tags.Count);
        Assert.Equal(0, result.Activities.Count);
        Assert.Equal(0, result.InputTypes.Count);
        Assert.Equal(0, result.Patterns.Count);
        Assert.Equal("1.0", result.Metadata.Version);
    }

    [Fact]
    public async Task ValidateBackupDataAsync_ShouldReturnValid_ForValidBackupData()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);

        var backupData = new BackupData
        {
            Metadata = new BackupMetadata { Version = "1.0" },
            InputTypes = new List<InputTypeBackup>
            {
                new InputTypeBackup { Name = "Text" }
            },
            Patterns = new List<PatternBackup>
            {
                new PatternBackup { Name = "Email", PatternValue = ".*@.*", Description = "Email pattern" }
            },
            Tags = new List<TagBackup>
            {
                new TagBackup 
                { 
                    TagName = "Work", 
                    InputTypeName = "Text",
                    PatternName = "Email"
                }
            },
            Activities = new List<ActivityBackup>
            {
                new ActivityBackup 
                { 
                    TagName = "Work",
                    DateCreated = DateTime.UtcNow,
                    DateStarted = DateTime.UtcNow
                }
            }
        };

        // Act
        var result = await service.ValidateBackupDataAsync(backupData);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateBackupDataAsync_ShouldReturnInvalid_ForDuplicateTagNames()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);

        var backupData = new BackupData
        {
            Tags = new List<TagBackup>
            {
                new TagBackup { TagName = "Work" },
                new TagBackup { TagName = "Work" } // Duplicate
            },
            Activities = new List<ActivityBackup>(),
            InputTypes = new List<InputTypeBackup>(),
            Patterns = new List<PatternBackup>()
        };

        // Act
        var result = await service.ValidateBackupDataAsync(backupData);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate tag names"));
    }

    [Fact]
    public async Task ClearDataAsync_ShouldReturnZero_WhenDatabaseIsEmpty()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var logger = new Mock<ILogger<BackupService>>();
        var service = new BackupService(context, logger.Object);

        // Act
        var result = await service.ClearDataAsync();

        // Assert
        Assert.Equal(0, result);
    }
}

public class BackupControllerTests
{
    [Fact]
    public async Task GetBackupInfo_ShouldReturnOk_WithBackupMetadata()
    {
        // Arrange
        var mockBackupService = new Mock<IBackupService>();
        var mockLogger = new Mock<ILogger<BackupController>>();
        
        var backupData = new BackupData
        {
            Metadata = new BackupMetadata
            {
                ExportDate = DateTime.UtcNow,
                Version = "1.0",
                TotalTags = 5,
                TotalActivities = 10
            }
        };

        mockBackupService.Setup(s => s.ExportDataAsync(It.IsAny<Guid?>()))
                        .ReturnsAsync(backupData);

        var controller = new BackupController(mockBackupService.Object, mockLogger.Object);

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
        
        mockBackupService.Setup(s => s.ClearDataAsync(It.IsAny<Guid?>()))
                        .ReturnsAsync(42);

        var controller = new BackupController(mockBackupService.Object, mockLogger.Object);

        // Act
        var result = await controller.ClearData();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Verify the service was called
        mockBackupService.Verify(s => s.ClearDataAsync(null), Times.Once);
    }
}
