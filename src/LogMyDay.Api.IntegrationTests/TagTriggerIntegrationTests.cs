using System.Net;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using ModelContextProtocol.Client;
using Refit;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// Tag Activity Relations triggers through the real host: created over REST, they set a reminder
/// when a value is logged over MCP, and are read over MCP.
/// </summary>
public class TagTriggerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TagTriggerIntegrationTests(CustomWebApplicationFactory factory)
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

    private HttpClient Http()
    {
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.ReadWriteKeyToken);

        return http;
    }

    [Fact]
    public async Task ATrigger_SetsAReminder_WhenItsTagIsLogged_AndIsReadOverMcp()
    {
        await using var mcp = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var (doseId, doseName) = await CreateTag(mcp, "Dose", InputTypeIds.Integer);
        var (effectId, _) = await CreateTag(mcp, "Effect", InputTypeIds.String);
        var api = RestService.For<ITagTriggerApi>(Http());
        var trigger = await api.CreateTagTrigger(new TagTriggerRequest
        {
            Name = Unique("Effect later"),
            IfTagId = doseId,
            PromptTagId = effectId,
            Timing = TagTriggerTiming.AfterEntry,
            OffsetMinutes = 120
        });
        Assert.Equal(("IsLogged", "AfterEntry", 120), (trigger.IfOperator, trigger.Timing, trigger.OffsetMinutes));

        // Logged over MCP a minute ago (the test host's user has no time zone: UTC).
        var at = DateTime.UtcNow.AddMinutes(-1);
        var logged = await mcp.CallAsync("log_value", new { tag = doseName, value = 100, dateTime = $"{at:yyyy-MM-ddTHH:mm}" });
        Assert.NotEqual(true, logged.IsError);

        var fire = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0).AddMinutes(120);
        var reminders = McpTestClient.Json(await mcp.CallAsync("list_reminders", new { date = $"{fire:yyyy-MM-dd}" }));
        var reminder = Assert.Single(reminders.EnumerateArray(), r => r.TryGetProperty("triggerId", out var id) && id.ValueKind == System.Text.Json.JsonValueKind.Number && id.GetInt32() == trigger.Id);
        Assert.Equal((trigger.Name, $"{fire:HH:mm}:00"), (reminder.GetProperty("triggerName").GetString(), reminder.GetProperty("notifyAt").GetString()));

        Assert.Equal(1, (await api.GetTagTriggerById(trigger.Id)).OpenReminderCount);
        var listed = McpTestClient.Json(await mcp.CallAsync("list_tag_triggers", new { }));
        Assert.Contains(listed.EnumerateArray(), t => t.GetProperty("id").GetInt32() == trigger.Id && t.GetProperty("promptTagId").GetInt32() == effectId);
        var one = McpTestClient.Json(await mcp.CallAsync("get_tag_trigger", new { triggerId = trigger.Id }));
        Assert.Equal(doseId, one.GetProperty("ifTagId").GetInt32());

        // A bad trigger is refused with a 400 that says why; deleting takes the open reminder.
        var bad = await Assert.ThrowsAsync<ApiException>(() => api.CreateTagTrigger(new TagTriggerRequest
        {
            Name = "Too soon",
            IfTagId = doseId,
            PromptTagId = effectId,
            OffsetMinutes = 1
        }));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("24 hours", bad.Content);

        await api.DeleteTagTrigger(trigger.Id);
        var after = McpTestClient.Json(await mcp.CallAsync("list_reminders", new { date = $"{fire:yyyy-MM-dd}" }));
        Assert.DoesNotContain(after.EnumerateArray(), r => r.GetProperty("title").GetString() == trigger.Name);
        Assert.Equal(HttpStatusCode.NotFound, (await Http().GetAsync($"/api/tag-triggers/{trigger.Id}")).StatusCode);
    }
}
