using System.Net;
using System.Net.Http.Json;
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

    // Exact times, so the reminder counts from the logged time and the test can name it.
    private static async Task<(int Id, string Name)> CreateTag(McpClient client, string prefix, int inputTypeId)
    {
        var name = Unique(prefix);
        var result = await client.CallAsync("create_tag", new { name, inputTypeId, isRepeatable = true, timeGranularity = "Exact" });
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

        // Logged over MCP a minute ago on the user's wall clock (the default time zone, not UTC).
        var zone = McpTestClient.Json(await mcp.CallAsync("server_info", new { })).GetProperty("user").GetProperty("timeZone").GetString()!;
        var at = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, zone), DateTimeKind.Unspecified).AddMinutes(-1);
        var logged = await mcp.CallAsync("log_value", new { tag = doseName, value = 100, dateTime = $"{at:yyyy-MM-ddTHH:mm}" });
        Assert.NotEqual(true, logged.IsError);

        var fire = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0).AddMinutes(120);
        var reminders = McpTestClient.Json(await mcp.CallAsync("list_reminders", new { date = $"{fire:yyyy-MM-dd}" }));
        var reminder = Assert.Single(reminders.EnumerateArray(), r => r.TryGetProperty("triggerId", out var id) && id.ValueKind == System.Text.Json.JsonValueKind.Number && id.GetInt32() == trigger.Id);
        Assert.Equal((trigger.Name, $"{fire:HH:mm}:00"), (reminder.GetProperty("triggerName").GetString(), reminder.GetProperty("notifyAt").GetString()));

        // The log_value result told about it.
        var told = Assert.Single(McpTestClient.Json(logged).GetProperty("reminders").EnumerateArray());
        Assert.Equal((reminder.GetProperty("id").GetInt32(), trigger.Id, $"{fire:HH:mm}:00", $"{fire:yyyy-MM-dd}"),
            (told.GetProperty("reminderId").GetInt32(), told.GetProperty("triggerId").GetInt32(), told.GetProperty("time").GetString(), told.GetProperty("day").GetString()));

        // A REST save reports only what it set: a second trigger's first reminder, not the first
        // trigger's, which already has one today. A save that sets nothing leaves the field out.
        var (laterId, _) = await CreateTag(mcp, "Later", InputTypeIds.String);
        var second = await api.CreateTagTrigger(new TagTriggerRequest { Name = Unique("Later"), IfTagId = doseId, PromptTagId = laterId, OffsetMinutes = 60 });
        async Task<System.Text.Json.JsonElement> PostDose()
        {
            var posted = await Http().PostAsJsonAsync("/api/activities", new ActivityRequest { PrimaryTagId = doseId, DateStarted = at, Description = "50" });
            posted.EnsureSuccessStatusCode();

            return System.Text.Json.JsonDocument.Parse(await posted.Content.ReadAsStringAsync()).RootElement;
        }

        var reported = Assert.Single((await PostDose()).GetProperty("reminders").EnumerateArray());
        Assert.Equal(second.Id, reported.GetProperty("triggerId").GetInt32());
        Assert.False((await PostDose()).TryGetProperty("reminders", out _));
        await api.DeleteTagTrigger(second.Id);

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

    [Fact]
    public async Task CompletingAReminderOrATodo_ReportsTheReminderItsLoggedValueSet()
    {
        await using var mcp = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var (doseId, _) = await CreateTag(mcp, "Dose", InputTypeIds.Integer);
        var (effectId, _) = await CreateTag(mcp, "Effect", InputTypeIds.String);
        var (walkedId, _) = await CreateTag(mcp, "Walked", InputTypeIds.String);
        var (moodId, _) = await CreateTag(mcp, "Mood", InputTypeIds.String);
        var triggers = RestService.For<ITagTriggerApi>(Http());
        var afterDose = await triggers.CreateTagTrigger(new TagTriggerRequest { Name = Unique("Effect later"), IfTagId = doseId, PromptTagId = effectId, OffsetMinutes = 120 });
        var afterWalk = await triggers.CreateTagTrigger(new TagTriggerRequest { Name = Unique("Mood later"), IfTagId = walkedId, PromptTagId = moodId, OffsetMinutes = 60 });

        // Over REST: completing a reminder logs its notes to the dose tag.
        var reminders = RestService.For<IReminderApi>(Http());
        var reminder = await reminders.CreateReminder(new ReminderRequest { Title = Unique("Dose"), Notes = "100", CompletionTagId = doseId });
        var completed = await reminders.CompleteReminder(reminder.Id, new ReminderCompleteRequest { DoneAt = DateTime.UtcNow });
        Assert.Equal(afterDose.Id, Assert.Single(completed.Reminders!).TriggerId);

        // Over MCP: completing a todo item logs its title to the list's tag.
        var todos = RestService.For<ITodoApi>(Http());
        var list = await todos.CreateTodoList(new TodoListRequest { Name = Unique("Walks"), CompletionTagId = walkedId });
        var item = await todos.CreateTodoItem(new TodoItemRequest { ListId = list.Id, Title = "Park" });
        var done = await mcp.CallAsync("complete_todo_item", new { itemId = item.Id });
        Assert.NotEqual(true, done.IsError);
        var told = Assert.Single(McpTestClient.Json(done).GetProperty("reminders").EnumerateArray());
        Assert.Equal(afterWalk.Id, told.GetProperty("triggerId").GetInt32());

        await triggers.DeleteTagTrigger(afterDose.Id);
        await triggers.DeleteTagTrigger(afterWalk.Id);
    }
}
