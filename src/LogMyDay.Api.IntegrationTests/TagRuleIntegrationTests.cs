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

    [Fact]
    public async Task RestApi_PreviewAndRecompute_CalculatePastDays()
    {
        await using var mcp = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var (sourceId, sourceName) = await CreateTag(mcp, "Dose", InputTypeIds.Integer);
        var (targetId, targetName) = await CreateTag(mcp, "Sum", InputTypeIds.Decimal);
        var past = DateTime.UtcNow.Date.AddDays(-10);
        await mcp.CallAsync("log_value", new { tag = sourceName, value = 4, dateTime = $"{past:yyyy-MM-dd}T09:00" });

        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.ReadWriteKeyToken);
        var api = Refit.RestService.For<LogMyDay.Shared.Interfaces.ITagRuleApi>(http);

        var rule = await api.CreateTagRule(new TagRuleRequest
        {
            Name = "Dose sum",
            TargetTagId = targetId,
            Sources = [new() { SourceTagId = sourceId, Factor = 0.5 }]
        });
        Assert.Equal(0, rule.ResultCount); // starts today; the past is not calculated yet

        var from = DateOnly.FromDateTime(past);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var preview = await api.PreviewTagRule(rule.Id, from, today);
        Assert.Equal(from, preview.From);
        Assert.Equal(1, preview.Created);

        var run = await api.RecomputeTagRule(rule.Id, new TagRuleRecomputeRequest { From = from, To = today });
        Assert.Equal(1, run.Created);

        var results = McpTestClient.Json(await mcp.CallAsync("list_activities", new { tag = targetName }));
        Assert.Equal("2", Assert.Single(results.GetProperty("items").EnumerateArray()).GetProperty("value").GetString());
        Assert.Equal(1, (await api.GetTagRuleById(rule.Id)).ResultCount);
    }
}
