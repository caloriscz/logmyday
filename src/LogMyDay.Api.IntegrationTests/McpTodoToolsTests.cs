using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>Todo list and item tools through the real endpoint.</summary>
public class McpTodoToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpTodoToolsTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task Lists_CreateUpdateGet_AndCompletionLogsTheItemTitle()
    {
        await using var client = await WriteClient();
        var tagId = Ok(await client.CallAsync("create_tag", new { name = Unique("Chores"), inputTypeId = InputTypeIds.String })).GetProperty("tag").GetProperty("id").GetInt32();

        var list = Ok(await client.CallAsync("create_todo_list", new { name = Unique("Home"), completionTagId = tagId, showOnHomepage = true }));
        var listId = list.GetProperty("id").GetInt32();
        Assert.Equal(tagId, list.GetProperty("completionTagId").GetInt32());
        Assert.True(list.GetProperty("showOnHomepage").GetBoolean());

        var renamed = Ok(await client.CallAsync("update_todo_list", new { listId, name = "Renamed home" }));
        Assert.Equal("Renamed home", renamed.GetProperty("name").GetString());
        Assert.Equal(tagId, renamed.GetProperty("completionTagId").GetInt32());

        var item = Ok(await client.CallAsync("create_todo_item", new { listId, title = "Water the plants", dueDate = "2026-04-10", notifyAt = "18:30" }));
        var itemId = item.GetProperty("id").GetInt32();
        Assert.Equal(new DateTime(2026, 4, 10), item.GetProperty("dueDate").GetDateTime());
        Assert.Equal("18:30:00", item.GetProperty("notifyAt").GetString());
        Assert.Equal("None", item.GetProperty("recurrenceType").GetString());

        var done = Ok(await client.CallAsync("complete_todo_item", new { itemId, doneAt = "2026-04-10T19:00" }));
        Assert.True(done.GetProperty("isDone").GetBoolean());
        var rows = Ok(await client.CallAsync("list_activities", new { tagId, from = "2026-04-10", to = "2026-04-10" }));
        Assert.Equal(1, rows.GetProperty("totalCount").GetInt32());
        Assert.Equal("Water the plants", rows.GetProperty("items")[0].GetProperty("value").GetString());

        var fetched = Ok(await client.CallAsync("get_todo_list", new { listId }));
        Assert.Contains(fetched.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt32() == itemId && i.GetProperty("isDone").GetBoolean());

        Ok(await client.CallAsync("reopen_todo_item", new { itemId }));
        Assert.Equal(1, Ok(await client.CallAsync("list_activities", new { tagId, from = "2026-04-10", to = "2026-04-10" })).GetProperty("totalCount").GetInt32());

        Assert.Contains(Ok(await client.CallAsync("list_todo_lists", new { date = "2026-04-10" })).EnumerateArray(), l => l.GetProperty("id").GetInt32() == listId);
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("create_todo_list", new { name = "x", completionTagId = 999_999 })));
    }

    [Fact]
    public async Task Items_UpdateSkipUnskipReorderDelete()
    {
        await using var client = await WriteClient();
        var listId = Ok(await client.CallAsync("create_todo_list", new { name = Unique("Errands") })).GetProperty("id").GetInt32();
        var a = Ok(await client.CallAsync("create_todo_item", new { listId, title = "A", displayOrder = 0 })).GetProperty("id").GetInt32();
        var b = Ok(await client.CallAsync("create_todo_item", new { listId, title = "B", displayOrder = 1, startDate = "2026-04-01" })).GetProperty("id").GetInt32();

        var updated = Ok(await client.CallAsync("update_todo_item", new { itemId = b, title = "B2", startDate = "", recurrenceType = "Daily" }));
        Assert.Equal("B2", updated.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("startDate").ValueKind);
        Assert.Equal("Daily", updated.GetProperty("recurrenceType").GetString());

        // Only recurring items carry a skipped state; b is Daily now, a is a one-off.
        Assert.True(Ok(await client.CallAsync("skip_todo_item", new { itemId = b, date = "2026-04-02" })).GetProperty("isSkipped").GetBoolean());
        Assert.False(Ok(await client.CallAsync("unskip_todo_item", new { itemId = b })).GetProperty("isSkipped").GetBoolean());
        Assert.False(Ok(await client.CallAsync("skip_todo_item", new { itemId = a })).GetProperty("isSkipped").GetBoolean());

        var reordered = Ok(await client.CallAsync("reorder_todo_items", new { listId, orderedItems = new[] { new { id = b, displayOrder = 0 }, new { id = a, displayOrder = 1 } } }));
        Assert.Equal([b, a], reordered.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));

        Ok(await client.CallAsync("delete_todo_item", new { itemId = a }));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("delete_todo_item", new { itemId = a })));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("update_todo_item", new { itemId = 999_999, title = "x" })));
    }

    [Fact]
    public async Task DeleteList_NeedsTheSentinel_AndReportsTheItemCount()
    {
        await using var client = await WriteClient();
        var listId = Ok(await client.CallAsync("create_todo_list", new { name = Unique("Doomed") })).GetProperty("id").GetInt32();
        await client.CallAsync("create_todo_item", new { listId, title = "one" });
        await client.CallAsync("create_todo_item", new { listId, title = "two" });

        var refused = await client.CallAsync("delete_todo_list", new { listId });
        Assert.Equal(McpErrorMapper.ConfirmationRequired, ErrorCode(refused));
        Assert.Equal(TodoTools.DeleteSentinelPrefix + listId, McpTestClient.Json(refused).GetProperty("expected").GetString());
        Assert.Contains("2 items", McpTestClient.Json(refused).GetProperty("impact").GetString());
        Assert.NotEqual(true, (await client.CallAsync("get_todo_list", new { listId })).IsError);

        var deleted = Ok(await client.CallAsync("delete_todo_list", new { listId, confirm = TodoTools.DeleteSentinelPrefix + listId }));
        Assert.Equal(2, deleted.GetProperty("itemsDeleted").GetInt32());
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("get_todo_list", new { listId })));
    }
}
