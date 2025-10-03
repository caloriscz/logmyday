using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Tests;

public class TagServiceTests
{
    [Fact]
    public async Task Create_ShouldAddTagAndReturnId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: "TagService_Create_Test")
            .Options;
        using var context = new LogMyDayDbContext(options);
        var loggerMock = new Mock<ILogger<TagService>>();
        var service = new TagService(context, loggerMock.Object);
        var tagRequest = new TagRequest { Tag = "TestTag", TypeId = 1 };
        var userId = Guid.NewGuid(); // Add a userId for the required parameter

        // Act
        var tagId = await service.Create(tagRequest, userId);

        // Assert
        var tag = await context.Tags.FindAsync(tagId);
        Assert.NotNull(tag);
        Assert.Equal("TestTag", tag.TagName);
        Assert.Equal(1, tag.InputTypeId);
    }

    [Fact]
    public async Task Create_ShouldPersistExtendedMetadata()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: "TagService_Metadata_Test")
            .Options;
        using var context = new LogMyDayDbContext(options);
        var loggerMock = new Mock<ILogger<TagService>>();
        var service = new TagService(context, loggerMock.Object);
        var request = new TagRequest
        {
            Tag = "Duration",
            TypeId = 0,
            IsRequired = true,
            IsRepeatable = false,
            TimeGranularity = TimeGranularity.Exact,
            IsRange = true,
            UnitId = 5,
            MinValue = 1.5,
            MaxValue = 10.5,
            Step = 0.5,
            DefaultValue = "2",
            OptionListId = 3
        };
        var userId = Guid.NewGuid();

        // Act
        var tagId = await service.Create(request, userId);
        var persisted = await context.Tags.FirstAsync(t => t.Id == tagId);

        // Assert
        Assert.Equal(request.UnitId, persisted.UnitId);
        Assert.Equal(request.MinValue, persisted.MinValue);
        Assert.Equal(request.MaxValue, persisted.MaxValue);
        Assert.Equal(request.Step, persisted.Step);
        Assert.Equal(request.DefaultValue, persisted.DefaultValue);
        Assert.Equal(request.OptionListId, persisted.OptionListId);
    }
}