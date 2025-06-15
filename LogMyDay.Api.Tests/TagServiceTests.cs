using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using LogMyDay.Api.Application.Services;
using LogMyDay.Shared.DTOs;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;

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

        // Act
        var tagId = await service.Create(tagRequest);

        // Assert
        var tag = await context.Tags.FindAsync(tagId);
        Assert.NotNull(tag);
        Assert.Equal("TestTag", tag.TagName);
        Assert.Equal(1, tag.InputTypeId);
    }
}