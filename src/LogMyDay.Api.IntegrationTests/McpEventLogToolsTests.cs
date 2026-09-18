using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>Event log tools through the real endpoint: agent entries, category filters, the purge sentinel.</summary>
public class McpEventLogToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpEventLogToolsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

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
    public async Task RecordEvent_IsPrefixed_AndFoundByQueryButNotUnderMcp()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var marker = Guid.NewGuid().ToString("N");

        var recorded = Ok(await client.CallAsync("record_event", new { message = $"felt great after the run {marker}", level = "Error" }));
        Assert.Equal($"Agent: felt great after the run {marker}", recorded.GetProperty("message").GetString());

        var page = Ok(await client.CallAsync("query_event_log", new { messageContains = marker }));
        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("Error", row.GetProperty("level").GetString());
        Assert.StartsWith(EventLogTools.AgentPrefix, row.GetProperty("message").GetString());
        // Non-admin users do not get the detail column.
        Assert.Equal(JsonValueKind.Null, row.GetProperty("detail").ValueKind);

        var underMcp = Ok(await client.CallAsync("query_event_log", new { messageContains = marker, category = "Mcp" }));
        Assert.Equal(0, underMcp.GetProperty("totalCount").GetInt32());
        // The write itself is audited under Mcp.
        var audit = Ok(await client.CallAsync("query_event_log", new { messageContains = "record_event", category = "Mcp", pageSize = 5 }));
        Assert.True(audit.GetProperty("totalCount").GetInt32() >= 1);

        Assert.Equal(1, Ok(await client.CallAsync("count_events", new { messageContains = marker })).GetProperty("count").GetInt32());
        Assert.Equal(0, Ok(await client.CallAsync("count_events", new { messageContains = marker, level = "Info" })).GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task RecordEvent_ValidatesInput()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("record_event", new { message = "  " })));
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("record_event", new { message = new string('x', 600) })));
    }

    [Fact]
    public async Task Query_FiltersByUtcRange_AndClampsPageSize()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var marker = Guid.NewGuid().ToString("N");
        Ok(await client.CallAsync("record_event", new { message = marker }));

        var recent = Ok(await client.CallAsync("query_event_log", new { messageContains = marker, from = DateTime.UtcNow.AddMinutes(-5).ToString("o"), pageSize = 9999 }));
        Assert.Equal(1, recent.GetProperty("totalCount").GetInt32());
        Assert.Equal(PageLimits.MaxPageSize, recent.GetProperty("pageSize").GetInt32());

        var old = Ok(await client.CallAsync("query_event_log", new { messageContains = marker, to = "2020-01-01T00:00:00Z" }));
        Assert.Equal(0, old.GetProperty("totalCount").GetInt32());

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("query_event_log", new { from = "yesterday" })));
    }

    [Fact]
    public async Task Purge_AllNeedsTheSentinel_OlderThanDoesNot()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var marker = Guid.NewGuid().ToString("N");
        Ok(await client.CallAsync("record_event", new { message = marker }));

        var refused = await client.CallAsync("purge_event_log");
        Assert.Equal(McpErrorMapper.ConfirmationRequired, ErrorCode(refused));
        Assert.Equal(EventLogTools.PurgeSentinel, McpTestClient.Json(refused).GetProperty("expected").GetString());
        Assert.Equal(1, Ok(await client.CallAsync("count_events", new { messageContains = marker })).GetProperty("count").GetInt32());

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("purge_event_log", new { olderThanDays = -1 })));

        // The delete itself is EventLogService.DeleteEvents' ExecuteDeleteAsync, which the InMemory
        // provider cannot run; the sentinel gate above is what this tool adds. Older-than needs no
        // sentinel: the call reaches the service (and fails only on the provider here).
        var trimmed = await client.CallAsync("purge_event_log", new { olderThanDays = 3650 });
        Assert.NotEqual(McpErrorMapper.ConfirmationRequired, McpTestClient.Json(trimmed).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ReadOnlyKey_CanQueryButNotRecord()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        Ok(await client.CallAsync("count_events"));
        var names = (await client.ListToolsAsync()).Select(t => t.Name).ToHashSet();
        Assert.Contains("query_event_log", names);
        Assert.DoesNotContain("record_event", names);
        Assert.DoesNotContain("purge_event_log", names);
    }
}
