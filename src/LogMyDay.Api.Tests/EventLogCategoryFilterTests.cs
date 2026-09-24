using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Categories are message-prefix conventions; the MCP audit prefix must land in its own bucket and
/// stay out of every other one, and agent-authored "Agent: " events must not pass as audit rows.
/// </summary>
public class EventLogCategoryFilterTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static LogMyDayDbContext NewContext(string name)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        var context = new LogMyDayDbContext(options);

        context.Users.Add(new User { Id = UserId, Email = "e@test.com", PasswordHash = "x" });
        context.EventLogs.AddRange(
            Row("MCP delete_tag via lmd_abcd1234: ok"),
            Row("MCP log_value via lmd_abcd1234: error"),
            Row("Agent: felt great after the run"),
            Row("Activity created: Exercise"),
            Row("Reminder fired: Vitamin D"),
            Row("Todo list completed: Groceries"),
            Row("[reminder-diag] boot re-arm"));
        context.SaveChanges();

        return context;
    }

    private static EventLog Row(string message) => new() { UserId = UserId, Level = EventLogLevel.Info, Message = message };

    private static async Task<List<string>> Messages(LogMyDayDbContext context, EventLogCategoryFilter category)
    {
        var service = new EventLogService(context, new Mock<ILogger<EventLogService>>().Object);
        var page = await service.GetPaged(1, 50, UserId, isAdmin: false, categoryFilter: category);

        return page.Items.Select(i => i.Message).ToList();
    }

    [Fact]
    public async Task Mcp_ReturnsOnlyAuditRows()
    {
        using var context = NewContext(nameof(Mcp_ReturnsOnlyAuditRows));

        var messages = await Messages(context, EventLogCategoryFilter.Mcp);

        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.StartsWith("MCP ", m));
    }

    [Theory]
    [InlineData(EventLogCategoryFilter.Activity)]
    [InlineData(EventLogCategoryFilter.Reminder)]
    [InlineData(EventLogCategoryFilter.TodoList)]
    // ReminderDiag/NoDiagnostics use an escaped "[" pattern the InMemory provider cannot evaluate.
    public async Task OtherCategories_ExcludeAuditRows(EventLogCategoryFilter category)
    {
        using var context = NewContext($"{nameof(OtherCategories_ExcludeAuditRows)}_{category}");

        var messages = await Messages(context, category);

        Assert.Single(messages);
        Assert.DoesNotContain(messages, m => m.StartsWith("MCP "));
    }

    [Fact]
    public async Task Count_HonoursTheMcpCategory()
    {
        using var context = NewContext(nameof(Count_HonoursTheMcpCategory));
        var service = new EventLogService(context, new Mock<ILogger<EventLogService>>().Object);

        Assert.Equal(2, await service.GetCount(UserId, categoryFilter: EventLogCategoryFilter.Mcp));
    }
}
