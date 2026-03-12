using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace LogMyDay.Api.Tests;

public class ScanMappingServiceTests
{
    private static LogMyDayDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new LogMyDayDbContext(options);
    }

    private static ScanMappingService CreateService(LogMyDayDbContext context)
    {
        var logger = new Mock<ILogger<ScanMappingService>>();

        return new ScanMappingService(context, logger.Object);
    }

    private static Tag CreateTag(int id, string name, Guid userId)
        => new() { Id = id, TagName = name, UserId = userId };

    private static ScanMapping CreateMapping(int id, Guid userId, string code, int tagId, string? displayName = null, bool isActive = true)
        => new()
        {
            Id = id,
            UserId = userId,
            CodeValue = code,
            CodeType = CodeType.Barcode,
            TagId = tagId,
            DisplayName = displayName,
            IsActive = isActive,
            DateCreated = DateTime.UtcNow
        };

    [Fact]
    public async Task GetAll_ReturnsOnlyUserMappings()
    {
        using var context = CreateContext(nameof(GetAll_ReturnsOnlyUserMappings));
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var tag1 = CreateTag(1, "Magnesium", user1);
        var tag2 = CreateTag(2, "Vitamin D", user2);
        context.Tags.AddRange(tag1, tag2);
        context.ScanMappings.AddRange(
            CreateMapping(1, user1, "111", 1),
            CreateMapping(2, user1, "222", 1),
            CreateMapping(3, user2, "333", 2));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetAll(user1);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.CodeValue == "333");
    }

    [Fact]
    public async Task GetAll_OrdersByDisplayNameThenTagName()
    {
        using var context = CreateContext(nameof(GetAll_OrdersByDisplayNameThenTagName));
        var userId = Guid.NewGuid();
        var tag1 = CreateTag(1, "Zinc", userId);
        var tag2 = CreateTag(2, "A-Tag", userId);
        context.Tags.AddRange(tag1, tag2);
        context.ScanMappings.AddRange(
            CreateMapping(1, userId, "code1", 1, displayName: null),
            CreateMapping(2, userId, "code2", 2, displayName: "A-Display"));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetAll(userId);

        Assert.Equal("A-Display", result[0].DisplayName);
        Assert.Null(result[1].DisplayName);
    }

    [Fact]
    public async Task GetById_ReturnsMapping()
    {
        using var context = CreateContext(nameof(GetById_ReturnsMapping));
        var userId = Guid.NewGuid();
        var tag = CreateTag(1, "Iron", userId);
        context.Tags.Add(tag);
        context.ScanMappings.Add(CreateMapping(1, userId, "abc123", 1, displayName: "My Iron"));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetById(1, userId);

        Assert.Equal("abc123", result.CodeValue);
        Assert.Equal("My Iron", result.DisplayName);
    }

    [Fact]
    public async Task GetById_OtherUsersMapping_ThrowsKeyNotFoundException()
    {
        using var context = CreateContext(nameof(GetById_OtherUsersMapping_ThrowsKeyNotFoundException));
        var userId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var tag = CreateTag(1, "Iron", userId);
        context.Tags.Add(tag);
        context.ScanMappings.Add(CreateMapping(1, userId, "abc123", 1));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetById(1, otherUser));
    }

    [Fact]
    public async Task Lookup_ExistingCode_ReturnsMappingWithTag()
    {
        using var context = CreateContext(nameof(Lookup_ExistingCode_ReturnsMappingWithTag));
        var userId = Guid.NewGuid();
        var tag = CreateTag(1, "Calcium", userId);
        context.Tags.Add(tag);
        var mapping = CreateMapping(1, userId, "1234567890", 1, displayName: "Daily Calcium");
        mapping.DefaultDescription = "500";
        context.ScanMappings.Add(mapping);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.Lookup("1234567890", userId);

        Assert.True(result.Found);
        Assert.NotNull(result.Mapping);
        Assert.Equal("1234567890", result.Mapping!.CodeValue);
        Assert.NotNull(result.Tag);
        Assert.Equal("Calcium", result.Tag!.Title);
    }

    [Fact]
    public async Task Lookup_UnknownCode_ReturnsNotFound()
    {
        using var context = CreateContext(nameof(Lookup_UnknownCode_ReturnsNotFound));
        var userId = Guid.NewGuid();

        var service = CreateService(context);
        var result = await service.Lookup("unknown-code", userId);

        Assert.False(result.Found);
        Assert.Null(result.Mapping);
    }

    [Fact]
    public async Task Lookup_InactiveMapping_ReturnsNotFound()
    {
        using var context = CreateContext(nameof(Lookup_InactiveMapping_ReturnsNotFound));
        var userId = Guid.NewGuid();
        var tag = CreateTag(1, "B12", userId);
        context.Tags.Add(tag);
        context.ScanMappings.Add(CreateMapping(1, userId, "inactive-code", 1, isActive: false));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.Lookup("inactive-code", userId);

        Assert.False(result.Found);
    }

    [Fact]
    public async Task Create_ValidRequest_PersistsMappingAndReturns()
    {
        using var context = CreateContext(nameof(Create_ValidRequest_PersistsMappingAndReturns));
        var userId = Guid.NewGuid();
        var tag = CreateTag(1, "Omega3", userId);
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new ScanMappingRequest
        {
            CodeValue = "omega3-barcode",
            CodeType = CodeType.Barcode,
            TagId = 1,
            DisplayName = "Fish Oil",
            DefaultDescription = "1000mg",
            IsActive = true
        };

        var result = await service.Create(request, userId);

        Assert.Equal("omega3-barcode", result.CodeValue);
        Assert.Equal("Fish Oil", result.DisplayName);
        Assert.Equal("1000mg", result.DefaultDescription);
        Assert.Equal(1, context.ScanMappings.Count());
    }

    [Fact]
    public async Task Create_TagBelongsToOtherUser_ThrowsKeyNotFoundException()
    {
        using var context = CreateContext(nameof(Create_TagBelongsToOtherUser_ThrowsKeyNotFoundException));
        var userId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var tag = CreateTag(1, "Tag", otherUser);
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new ScanMappingRequest { CodeValue = "code", TagId = 1 };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.Create(request, userId));
    }

    [Fact]
    public async Task Create_DuplicateCodeValue_ThrowsInvalidOperationException()
    {
        using var context = CreateContext(nameof(Create_DuplicateCodeValue_ThrowsInvalidOperationException));
        var userId = Guid.NewGuid();
        var tag = CreateTag(1, "Tag", userId);
        context.Tags.Add(tag);
        context.ScanMappings.Add(CreateMapping(1, userId, "duplicate-code", 1));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new ScanMappingRequest { CodeValue = "duplicate-code", TagId = 1 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Create(request, userId));
    }

    [Fact]
    public async Task Delete_ValidId_RemovesMapping()
    {
        using var context = CreateContext(nameof(Delete_ValidId_RemovesMapping));
        var userId = Guid.NewGuid();
        var tag = CreateTag(1, "Tag", userId);
        context.Tags.Add(tag);
        context.ScanMappings.Add(CreateMapping(1, userId, "code-to-delete", 1));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.Delete(1, userId);

        Assert.Equal(0, context.ScanMappings.Count());
    }

    [Fact]
    public async Task Delete_OtherUsersMapping_ThrowsKeyNotFoundException()
    {
        using var context = CreateContext(nameof(Delete_OtherUsersMapping_ThrowsKeyNotFoundException));
        var userId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var tag = CreateTag(1, "Tag", userId);
        context.Tags.Add(tag);
        context.ScanMappings.Add(CreateMapping(1, userId, "code", 1));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.Delete(1, otherUser));
    }
}
