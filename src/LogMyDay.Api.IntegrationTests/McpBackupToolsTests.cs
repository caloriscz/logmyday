using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Mcp.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// Backup tools through the real endpoint. The clear tools are only exercised up to their sentinel
/// refusal: the shared in-memory database is used by every other test class at the same time.
/// </summary>
public class McpBackupToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpBackupToolsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

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
    public async Task Info_Export_AndRestore_RoundTrip()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var name = Unique("Backup");
        Ok(await client.CallAsync("create_tag", new { name, inputTypeId = InputTypeIds.Integer }));
        Ok(await client.CallAsync("log_value", new { tag = name, value = 7, dateTime = "2026-08-01T10:00" }));

        var info = Ok(await client.CallAsync("get_backup_info"));
        Assert.True(info.GetProperty("metadata").GetProperty("totalTags").GetInt32() >= 1);
        Assert.True(info.GetProperty("metadata").GetProperty("totalActivities").GetInt32() >= 1);

        var export = Ok(await client.CallAsync("export_secure_backup"));
        Assert.Equal("2.1", export.GetProperty("version").GetString());
        Assert.Contains(export.GetProperty("tags").EnumerateArray(), t => t.GetProperty("tagName").GetString() == name);
        Assert.Contains(export.GetProperty("activities").EnumerateArray(), a => a.GetProperty("tagName").GetString() == name && a.GetProperty("description").GetString() == "7");
        Assert.DoesNotContain("passwordHash", export.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var refused = await client.CallAsync("restore_secure_backup", new { backup = export });
        Assert.Equal(McpErrorMapper.ConfirmationRequired, ErrorCode(refused));
        Assert.Equal(BackupTools.RestoreSentinel, McpTestClient.Json(refused).GetProperty("expected").GetString());
        Assert.Contains("activities", McpTestClient.Json(refused).GetProperty("impact").GetString());

        var restored = Ok(await client.CallAsync("restore_secure_backup", new { backup = export, confirm = BackupTools.RestoreSentinel }));
        Assert.True(restored.GetProperty("success").GetBoolean(), restored.GetRawText());
        // Restoring the user's own export is a no-op merge: the tag still exists exactly once.
        Assert.Equal(1, Ok(await client.CallAsync("list_tags", new { filter = name, filterType = "exact" })).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Validate_ChecksTheDocumentShape()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

        var result = Ok(await client.CallAsync("validate_backup", new { backup = new { metadata = new { version = "2.0" }, tags = Array.Empty<object>(), activities = Array.Empty<object>() } }));
        Assert.True(result.TryGetProperty("isValid", out _));

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("validate_backup", new { backup = "not an object" })));
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("restore_secure_backup", new { backup = 42 })));
    }

    [Fact]
    public async Task ClearUserData_RefusesWithoutTheSentinel()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var name = Unique("Keep");
        Ok(await client.CallAsync("create_tag", new { name, inputTypeId = InputTypeIds.Integer }));

        var refused = await client.CallAsync("clear_user_data");
        Assert.Equal(McpErrorMapper.ConfirmationRequired, ErrorCode(refused));
        Assert.Equal(BackupTools.ClearSentinel, McpTestClient.Json(refused).GetProperty("expected").GetString());
        Assert.Contains("tags", McpTestClient.Json(refused).GetProperty("impact").GetString());
        Assert.Equal(1, Ok(await client.CallAsync("list_tags", new { filter = name, filterType = "exact" })).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task AdminTools_AreHiddenAndRefused_ForANonAdminUser()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

        var names = (await client.ListToolsAsync()).Select(t => t.Name).ToHashSet();
        Assert.DoesNotContain("admin_export_backup", names);
        Assert.DoesNotContain("admin_clear_all_data", names);

        var ex = await Assert.ThrowsAsync<McpProtocolException>(() => client.CallAsync("admin_clear_all_data", new { confirm = BackupTools.ClearAllSentinel }));
        Assert.Contains("forbidden", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
