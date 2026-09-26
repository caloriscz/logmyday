using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// Tag Activity Relations through the real host: a rule's result follows source writes made
/// through MCP, and the computed tag refuses manual writes with a conflict.
/// </summary>
public class TagRuleIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TagRuleIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static async Task<(int Id, string Name)> CreateTag(McpClient client, string prefix, int inputTypeId)
    {
        var name = Unique(prefix);
        var result = await client.CallAsync("create_tag", new { name, inputTypeId, isRepeatable = true });
        Assert.NotEqual(true, result.IsError);

        return (McpTestClient.Json(result).GetProperty("tag").GetProperty("id").GetInt32(), name);
    }

    [Fact]
    public async Task RuleResult_FollowsMcpSourceWrites_AndComputedTagRefusesWrites()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var (sourceId, sourceName) = await CreateTag(client, "Pill", InputTypeIds.Integer);
        var (targetId, targetName) = await CreateTag(client, "Total", InputTypeIds.Decimal);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            var userId = await db.Users.Where(u => u.Email == CustomWebApplicationFactory.McpUserEmail).Select(u => u.Id).SingleAsync();
            var rules = scope.ServiceProvider.GetRequiredService<ITagRuleService>();
            await rules.Create(new TagRuleRequest
            {
                Name = "Pill total",
                TargetTagId = targetId,
                Sources = [new() { SourceTagId = sourceId, Factor = 2.5 }]
            }, userId);
        }

        // The rule is active from today, so log today (the user's zone is the seeded default).
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var logged = await client.CallAsync("log_value", new { tag = sourceName, value = 2, dateTime = $"{today}T09:00" });
        Assert.NotEqual(true, logged.IsError);

        var results = McpTestClient.Json(await client.CallAsync("list_activities", new { tag = targetName }));
        var row = Assert.Single(results.GetProperty("items").EnumerateArray());
        Assert.Equal("5", row.GetProperty("value").GetString());

        var refused = await client.CallAsync("log_value", new { tag = targetName, value = 1, dateTime = $"{today}T10:00" });
        Assert.True(refused.IsError);
        Assert.Equal("conflict", McpTestClient.Json(refused).GetProperty("code").GetString());
    }
}
