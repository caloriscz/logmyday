using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>The three prompts through the real endpoint: listed, rendered with their arguments, readable with a read-only key.</summary>
public class McpPromptsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpPromptsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string Text(GetPromptResult result)
    {
        var message = Assert.Single(result.Messages);
        Assert.Equal(Role.User, message.Role);

        return Assert.IsType<TextContentBlock>(message.Content).Text;
    }

    [Fact]
    public async Task PromptsList_HasTheThreePrompts_WithArguments()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var prompts = await client.ListPromptsAsync();

        Assert.Equal(["daily_review", "log_by_conversation", "weekly_summary"], prompts.Select(p => p.Name).OrderBy(n => n));
        var weekly = prompts.Single(p => p.Name == "weekly_summary");
        Assert.Equal(["tags", "weekEnding"], weekly.ProtocolPrompt.Arguments!.Select(a => a.Name).OrderBy(n => n));
        Assert.All(weekly.ProtocolPrompt.Arguments!, a => Assert.False(a.Required ?? false));
    }

    [Fact]
    public async Task DailyReview_RendersTheDate_AndDefaultsToToday()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var text = Text(await client.GetPromptAsync("daily_review", new Dictionary<string, object?> { ["date"] = "2026-09-18" }));
        Assert.StartsWith("Review my LogMyDay entries for 2026-09-18.", text);
        Assert.Contains("list_reminders for 2026-09-18", text);
        Assert.DoesNotContain("{date}", text);

        var today = Text(await client.GetPromptAsync("daily_review", new Dictionary<string, object?>()));
        var vienna = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")));
        Assert.Contains(vienna.ToString("yyyy-MM-dd"), today);
    }

    [Fact]
    public async Task WeeklySummary_ComputesBothRanges_AndTagSelection()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var given = Text(await client.GetPromptAsync("weekly_summary", new Dictionary<string, object?> { ["weekEnding"] = "2026-09-13", ["tags"] = "Sleep, Health:Vitamin D" }));
        Assert.Contains("seven days ending 2026-09-13", given);
        Assert.Contains("find_tag: Sleep, Health:Vitamin D,", given);
        Assert.Contains("from 2026-09-07 to 2026-09-13", given);
        Assert.Contains("for 2026-08-31 to 2026-09-06", given);
        Assert.DoesNotContain("{", given);

        var all = Text(await client.GetPromptAsync("weekly_summary", new Dictionary<string, object?> { ["weekEnding"] = "2026-09-13" }));
        Assert.Contains("Call list_tags", all);
        Assert.DoesNotContain("find_tag:", all);
    }

    [Fact]
    public async Task LogByConversation_IsStatic_AndNamesTheKeyTools()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var text = Text(await client.GetPromptAsync("log_by_conversation", new Dictionary<string, object?>()));

        Assert.Contains("find_tag", text);
        Assert.Contains("log_value", text);
        Assert.Contains("tag-day-locked", text);
    }
}
