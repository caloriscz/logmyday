using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Enums;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>Reminder tools through the real endpoint, including the activity side effects.</summary>
public class McpReminderToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpReminderToolsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private Task<McpClient> WriteClient() => McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static JsonElement Ok(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);

        return McpTestClient.Json(result);
    }

    private static string ErrorCode(CallToolResult result)
    {
        Assert.True(result.IsError, "expected an error result");

        return McpTestClient.Json(result).GetProperty("code").GetString()!;
    }

    private static async Task<int> CreateTag(McpClient client, string prefix, int inputTypeId = InputTypeIds.Integer)
    {
        return Ok(await client.CallAsync("create_tag", new { name = Unique(prefix), inputTypeId })).GetProperty("tag").GetProperty("id").GetInt32();
    }

    private static async Task<int> CountActivities(McpClient client, int tagId, string day)
    {
        return Ok(await client.CallAsync("list_activities", new { tagId, from = day, to = day })).GetProperty("totalCount").GetInt32();
    }

    [Fact]
    public async Task CreateUpdateList_HonoursTheMonitoringWindow_AndCoercesNoneToDaily()
    {
        await using var client = await WriteClient();
        var tagId = await CreateTag(client, "Thyroid");
        var title = Unique("Thyroid 50");

        var created = Ok(await client.CallAsync("create_reminder", new
        {
            title, notifyAt = "07:00", completionTagId = tagId, recurrenceType = "None", monitorToDate = "2020-01-01", notes = "50"
        }));
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal("Daily", created.GetProperty("recurrenceType").GetString());
        Assert.Equal("07:00:00", created.GetProperty("notifyAt").GetString());
        Assert.Equal(tagId, created.GetProperty("completionTagId").GetInt32());

        // Window ended in 2020: hidden by default, visible on request.
        Assert.DoesNotContain(Ok(await client.CallAsync("list_reminders")).EnumerateArray(), r => r.GetProperty("id").GetInt32() == id);
        Assert.Contains(Ok(await client.CallAsync("list_reminders", new { includeOutOfWindow = true })).EnumerateArray(), r => r.GetProperty("id").GetInt32() == id);

        var updated = Ok(await client.CallAsync("update_reminder", new { reminderId = id, monitorToDate = "", notes = "75" }));
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("monitorToDate").ValueKind);
        Assert.Equal("75", updated.GetProperty("notes").GetString());
        Assert.Equal(title, updated.GetProperty("title").GetString());
        Assert.Contains(Ok(await client.CallAsync("list_reminders")).EnumerateArray(), r => r.GetProperty("id").GetInt32() == id);

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("create_reminder", new { title = "bad time", notifyAt = "7pm" })));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("create_reminder", new { title = "bad tag", completionTagId = 999_999 })));
    }

    [Fact]
    public async Task Complete_LogsTheEncodedValueToTheCompletionTag_AndReopenKeepsIt()
    {
        await using var client = await WriteClient();
        var tagId = await CreateTag(client, "VitD");
        var id = Ok(await client.CallAsync("create_reminder", new { title = Unique("Vit D"), completionTagId = tagId })).GetProperty("id").GetInt32();

        var done = Ok(await client.CallAsync("complete_reminder", new { reminderId = id, doneAt = "2026-03-20T08:00", completionValue = "2000" }));
        Assert.True(done.GetProperty("isDone").GetBoolean());

        var rows = Ok(await client.CallAsync("list_activities", new { tagId, from = "2026-03-20", to = "2026-03-20" }));
        Assert.Equal(1, rows.GetProperty("totalCount").GetInt32());
        Assert.Equal("2000", rows.GetProperty("items")[0].GetProperty("value").GetString());

        var onThatDay = Ok(await client.CallAsync("get_reminder", new { reminderId = id, date = "2026-03-20" }));
        Assert.True(onThatDay.GetProperty("isDone").GetBoolean());

        // A value the tag cannot hold is refused before anything is written.
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("complete_reminder", new { reminderId = id, doneAt = "2026-03-21T08:00", completionValue = "lots" })));
        Assert.Equal(0, await CountActivities(client, tagId, "2026-03-21"));

        Ok(await client.CallAsync("reopen_reminder", new { reminderId = id }));
        Assert.Equal(1, await CountActivities(client, tagId, "2026-03-20"));
    }

    [Fact]
    public async Task Complete_WithValueButNoTag_IsInvalid()
    {
        await using var client = await WriteClient();
        var id = Ok(await client.CallAsync("create_reminder", new { title = Unique("Stretch") })).GetProperty("id").GetInt32();

        var result = await client.CallAsync("complete_reminder", new { reminderId = id, completionValue = 1 });

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(result));
        Assert.Contains("no completion tag", McpTestClient.Json(result).GetProperty("message").GetString());
        Assert.True(Ok(await client.CallAsync("complete_reminder", new { reminderId = id })).GetProperty("isDone").GetBoolean());
    }

    [Fact]
    public async Task Skip_WritesAZeroActivity_AndUnskipLeavesIt()
    {
        await using var client = await WriteClient();
        var tagId = await CreateTag(client, "Pain");
        var id = Ok(await client.CallAsync("create_reminder", new { title = Unique("Painkiller"), completionTagId = tagId })).GetProperty("id").GetInt32();

        var skipped = Ok(await client.CallAsync("skip_reminder", new { reminderId = id, date = "2026-03-22" }));
        Assert.True(skipped.GetProperty("isSkipped").GetBoolean());
        var rows = Ok(await client.CallAsync("list_activities", new { tagId, from = "2026-03-22", to = "2026-03-22" }));
        Assert.Equal(1, rows.GetProperty("totalCount").GetInt32());
        Assert.Equal("0", rows.GetProperty("items")[0].GetProperty("value").GetString());

        var unskipped = Ok(await client.CallAsync("unskip_reminder", new { reminderId = id, date = "2026-03-22" }));
        Assert.False(unskipped.GetProperty("isSkipped").GetBoolean());
        Assert.Equal(1, await CountActivities(client, tagId, "2026-03-22"));
    }

    [Fact]
    public async Task Complete_OnALockedDay_IsTagDayLocked()
    {
        await using var client = await WriteClient();
        var tagId = await CreateTag(client, "Locked");
        var id = Ok(await client.CallAsync("create_reminder", new { title = Unique("Locked med"), completionTagId = tagId })).GetProperty("id").GetInt32();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == CustomWebApplicationFactory.McpUserEmail);
            await scope.ServiceProvider.GetRequiredService<ITagDayLockService>()
                .Upsert(user.Id, new TagDayLockRequest { TagId = tagId, Date = new DateOnly(2026, 3, 23), IsLocked = true }, DayLockSetBy.User);
        }

        var result = await client.CallAsync("complete_reminder", new { reminderId = id, doneAt = "2026-03-23T12:00", completionValue = 1 });

        Assert.Equal(McpErrorMapper.TagDayLocked, ErrorCode(result));
        Assert.Equal(0, await CountActivities(client, tagId, "2026-03-23"));
        Assert.True(Ok(await client.CallAsync("get_reminder", new { reminderId = id, date = "2026-03-23" })).GetProperty("isTagDayLocked").GetBoolean());
    }

    [Fact]
    public async Task ReorderAndDelete()
    {
        await using var client = await WriteClient();
        var a = Ok(await client.CallAsync("create_reminder", new { title = Unique("A"), notifyAt = "09:00" })).GetProperty("id").GetInt32();
        var b = Ok(await client.CallAsync("create_reminder", new { title = Unique("B"), notifyAt = "09:00" })).GetProperty("id").GetInt32();

        var after = Ok(await client.CallAsync("reorder_reminders", new { items = new[] { new { id = b, displayOrder = 0 }, new { id = a, displayOrder = 1 } } }));
        var order = after.EnumerateArray().Where(r => r.GetProperty("id").GetInt32() is var x && (x == a || x == b)).Select(r => r.GetProperty("id").GetInt32()).ToList();
        Assert.Equal([b, a], order);

        Ok(await client.CallAsync("delete_reminder", new { reminderId = a }));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("get_reminder", new { reminderId = a })));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("delete_reminder", new { reminderId = a })));
    }
}
