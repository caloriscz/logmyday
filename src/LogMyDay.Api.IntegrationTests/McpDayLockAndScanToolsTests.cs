using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Mcp.Infrastructure;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>Day-lock and scan-mapping tools through the real endpoint.</summary>
public class McpDayLockAndScanToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpDayLockAndScanToolsTests(CustomWebApplicationFactory factory)
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

    private static async Task<(int Id, string Name)> CreateTag(McpClient client, string prefix)
    {
        var name = Unique(prefix);
        var tag = Ok(await client.CallAsync("create_tag", new { name, inputTypeId = InputTypeIds.Integer }));

        return (tag.GetProperty("tag").GetProperty("id").GetInt32(), name);
    }

    [Fact]
    public async Task DayLock_SetBlocksLogging_UnlockRowDiffersFromDelete()
    {
        await using var client = await WriteClient();
        var (tagId, name) = await CreateTag(client, "Pill");

        var before = Ok(await client.CallAsync("get_tag_day_lock", new { tagId, date = "2026-05-05" }));
        Assert.False(before.GetProperty("isLocked").GetBoolean());
        Assert.False(before.GetProperty("hasRow").GetBoolean());

        var locked = Ok(await client.CallAsync("set_tag_day_lock", new { tagId, date = "2026-05-05", isLocked = true, reason = "all doses taken" }));
        Assert.True(locked.GetProperty("isLocked").GetBoolean());
        Assert.Equal("User", locked.GetProperty("setBy").GetString());
        Assert.Equal("all doses taken", locked.GetProperty("reason").GetString());

        Assert.Contains(Ok(await client.CallAsync("list_tag_day_locks", new { date = "2026-05-05" })).EnumerateArray(),
            l => l.GetProperty("tagId").GetInt32() == tagId && l.GetProperty("isLocked").GetBoolean());
        Assert.Equal(McpErrorMapper.TagDayLocked, ErrorCode(await client.CallAsync("log_value", new { tag = name, value = 1, dateTime = "2026-05-05T12:00" })));

        // Explicit unlock keeps a row; logging works again.
        var unlocked = Ok(await client.CallAsync("set_tag_day_lock", new { tagId, date = "2026-05-05", isLocked = false }));
        Assert.False(unlocked.GetProperty("isLocked").GetBoolean());
        var state = Ok(await client.CallAsync("get_tag_day_lock", new { tagId, date = "2026-05-05" }));
        Assert.True(state.GetProperty("hasRow").GetBoolean());
        Assert.False(state.GetProperty("isLocked").GetBoolean());
        Ok(await client.CallAsync("log_value", new { tag = name, value = 1, dateTime = "2026-05-05T12:00" }));

        // Delete removes the row entirely; deleting again is not-found.
        Ok(await client.CallAsync("delete_tag_day_lock", new { tagId, date = "2026-05-05" }));
        Assert.False(Ok(await client.CallAsync("get_tag_day_lock", new { tagId, date = "2026-05-05" })).GetProperty("hasRow").GetBoolean());
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("delete_tag_day_lock", new { tagId, date = "2026-05-05" })));

        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("set_tag_day_lock", new { tagId = 999_999, isLocked = true })));
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("set_tag_day_lock", new { tagId, isLocked = true, reason = new string('x', 201) })));
    }

    [Fact]
    public async Task ScanMappings_RoundTrip_LookupAndDuplicateConflict()
    {
        await using var client = await WriteClient();
        var (tagId, name) = await CreateTag(client, "Scan");
        var code = Guid.NewGuid().ToString("N");

        var created = Ok(await client.CallAsync("create_scan_mapping", new { codeValue = code, tagId, codeType = "QRCode", displayName = "Vitamin box", defaultDescription = "1" }));
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal("QRCode", created.GetProperty("codeType").GetString());
        Assert.Equal(name, created.GetProperty("tagName").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());

        var found = Ok(await client.CallAsync("lookup_scan_code", new { codeValue = code }));
        Assert.True(found.GetProperty("found").GetBoolean());
        Assert.Equal(tagId, found.GetProperty("tag").GetProperty("id").GetInt32());
        Assert.False(Ok(await client.CallAsync("lookup_scan_code", new { codeValue = "no-such-code" })).GetProperty("found").GetBoolean());

        var duplicate = await client.CallAsync("create_scan_mapping", new { codeValue = code, tagId });
        Assert.Equal(McpErrorMapper.Conflict, ErrorCode(duplicate));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("create_scan_mapping", new { codeValue = Guid.NewGuid().ToString("N"), tagId = 999_999 })));

        var updated = Ok(await client.CallAsync("update_scan_mapping", new { mappingId = id, displayName = "", isActive = false }));
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("displayName").ValueKind);
        Assert.False(updated.GetProperty("isActive").GetBoolean());
        Assert.Equal(code, updated.GetProperty("codeValue").GetString());
        Assert.Equal("1", updated.GetProperty("defaultDescription").GetString());

        Assert.Contains(Ok(await client.CallAsync("list_scan_mappings")).EnumerateArray(), m => m.GetProperty("id").GetInt32() == id);
        Assert.Equal(id, Ok(await client.CallAsync("get_scan_mapping", new { mappingId = id })).GetProperty("id").GetInt32());

        Ok(await client.CallAsync("delete_scan_mapping", new { mappingId = id }));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("get_scan_mapping", new { mappingId = id })));
    }
}
