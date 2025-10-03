using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;

namespace LogMyDay.Api.Tests;

public class TagOptionListServiceTests
{
    [Fact]
    public async Task Create_ShouldPersistOptions()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase("OptionList_Create")
            .Options;
        using var context = new LogMyDayDbContext(options);

        var logger = Mock.Of<ILogger<TagOptionListService>>();
        var service = new TagOptionListService(context, logger);
        var request = new TagOptionListRequest
        {
            Name = "Mood",
            Options =
            {
                new TagOptionRequest { Value = "happy", DisplayName = "Happy" },
                new TagOptionRequest { Value = "sad", DisplayName = "Sad" }
            }
        };

        var userId = Guid.NewGuid();
        var id = await service.CreateAsync(request, userId);

        var list = await context.TagOptionLists.Include(l => l.Options).FirstAsync(l => l.Id == id);
        Assert.Equal("Mood", list.Name);
        Assert.Equal(userId, list.UserId);
        Assert.Equal(2, list.Options.Count);
    }

    [Fact]
    public async Task Update_ShouldSynchronizeOptions()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase("OptionList_Update")
            .Options;
        using var context = new LogMyDayDbContext(options);

        var list = new TagOptionList
        {
            Name = "Energy",
            UserId = Guid.NewGuid(),
            Options =
            {
                new TagOption { Value = "low", DisplayName = "Low" },
                new TagOption { Value = "medium", DisplayName = "Medium" }
            }
        };

        context.TagOptionLists.Add(list);
        await context.SaveChangesAsync();

        var logger = Mock.Of<ILogger<TagOptionListService>>();
        var service = new TagOptionListService(context, logger);

        var request = new TagOptionListRequest
        {
            Name = "Energy Level",
            Options =
            {
                new TagOptionRequest { Id = list.Options.First().Id, Value = "low", DisplayName = "Very Low" },
                new TagOptionRequest { Value = "high", DisplayName = "High" }
            }
        };

        await service.UpdateAsync(list.Id, request, list.UserId!.Value);

        var updated = await context.TagOptionLists.Include(l => l.Options).FirstAsync(l => l.Id == list.Id);
        Assert.Equal("Energy Level", updated.Name);
        Assert.Equal(2, updated.Options.Count);
        Assert.Contains(updated.Options, o => o.Value == "low" && o.DisplayName == "Very Low");
        Assert.Contains(updated.Options, o => o.Value == "high");
        Assert.DoesNotContain(updated.Options, o => o.Value == "medium");
    }
}
