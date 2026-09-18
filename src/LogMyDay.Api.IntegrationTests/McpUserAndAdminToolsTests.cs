using System.Text.Json;
using LogMyDay.Api.Application.Services;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>User and admin tools through the real endpoint, with a non-admin and an admin key.</summary>
public class McpUserAndAdminToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpUserAndAdminToolsTests(CustomWebApplicationFactory factory)
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

    // --- Self ---

    [Fact]
    public async Task GetMe_ReturnsTheUserAndTheKey()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

        var me = Ok(await client.CallAsync("get_me"));

        Assert.Equal(CustomWebApplicationFactory.McpUserEmail, me.GetProperty("user").GetProperty("email").GetString());
        Assert.False(me.GetProperty("user").GetProperty("isAdmin").GetBoolean());
        var key = me.GetProperty("apiKey");
        Assert.Equal("rw", key.GetProperty("name").GetString());
        Assert.Equal(CustomWebApplicationFactory.ReadWriteKeyToken[..ApiKeyService.PrefixLength], key.GetProperty("prefix").GetString());
        Assert.Equal("ReadWrite", key.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task UpdateMyPreferences_ValidatesAndMerges()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var before = Ok(await client.CallAsync("get_me")).GetProperty("user");
        var originalCulture = before.GetProperty("culture").GetString();
        var originalZone = before.GetProperty("timeZone").GetString();

        try
        {
            var updated = Ok(await client.CallAsync("update_my_preferences", new { displayName = "Karel", timeZone = "Europe/Prague", activityDisplayType = "Weekly", activitySortOrder = "group-asc" }));
            Assert.Equal("Karel", updated.GetProperty("displayName").GetString());
            Assert.Equal("Europe/Prague", updated.GetProperty("timeZone").GetString());
            Assert.Equal("weekly", updated.GetProperty("activityDisplayType").GetString());
            Assert.Equal("group-asc", updated.GetProperty("activitySortOrder").GetString());
            Assert.Equal(originalCulture, updated.GetProperty("culture").GetString());

            Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("update_my_preferences", new { timeZone = "Mars/Olympus" })));
            Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("update_my_preferences", new { culture = "xx-NOPE-zz" })));
            Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("update_my_preferences", new { activityDisplayType = "hourly" })));
            Assert.Equal("Europe/Prague", Ok(await client.CallAsync("get_me")).GetProperty("user").GetProperty("timeZone").GetString());

            var names = (await client.ListToolsAsync()).Select(t => t.Name).ToHashSet();
            Assert.DoesNotContain("change_password", names);
            Assert.DoesNotContain("admin_list_users", names);
        }
        finally
        {
            Ok(await client.CallAsync("update_my_preferences", new { displayName = "", culture = originalCulture, timeZone = originalZone, activityDisplayType = "daily", activitySortOrder = "desc" }));
        }
    }

    // --- Admin ---

    [Fact]
    public async Task AdminKey_ManagesUsers_WithTheDeleteSentinel()
    {
        await using var admin = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.AdminKeyToken);
        var email = $"new-{Guid.NewGuid():N}@example.com";

        var users = Ok(await admin.CallAsync("admin_list_users")).EnumerateArray().Select(u => u.GetProperty("email").GetString()).ToList();
        Assert.Contains(CustomWebApplicationFactory.McpUserEmail, users);
        Assert.Contains(CustomWebApplicationFactory.AdminUserEmail, users);

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await admin.CallAsync("admin_create_user", new { email, password = "short" })));
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await admin.CallAsync("admin_create_user", new { email = "not-an-email", password = "long-enough-password" })));

        var created = Ok(await admin.CallAsync("admin_create_user", new { email, password = "long-enough-password", displayName = "New", culture = "de-DE", timeZone = "Europe/Berlin" }));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("de-DE", created.GetProperty("culture").GetString());
        Assert.False(created.GetProperty("isAdmin").GetBoolean());
        Assert.Equal(McpErrorMapper.Conflict, ErrorCode(await admin.CallAsync("admin_create_user", new { email, password = "long-enough-password" })));

        var updated = Ok(await admin.CallAsync("admin_update_user", new { userId = id, displayName = "Renamed", isAdmin = true }));
        Assert.Equal("Renamed", updated.GetProperty("displayName").GetString());
        Assert.True(updated.GetProperty("isAdmin").GetBoolean());
        Assert.Equal("Europe/Berlin", updated.GetProperty("timeZone").GetString());

        Assert.True(Ok(await admin.CallAsync("admin_reset_password", new { userId = id, newPassword = "another-long-password" })).GetProperty("reset").GetBoolean());
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await admin.CallAsync("admin_reset_password", new { userId = id, newPassword = "short" })));

        var refused = await admin.CallAsync("admin_delete_user", new { userId = id });
        Assert.Equal(McpErrorMapper.ConfirmationRequired, ErrorCode(refused));
        Assert.Equal(AdminTools.DeleteSentinelPrefix + email, McpTestClient.Json(refused).GetProperty("expected").GetString());

        Ok(await admin.CallAsync("admin_delete_user", new { userId = id, confirm = AdminTools.DeleteSentinelPrefix + email }));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await admin.CallAsync("admin_update_user", new { userId = id, displayName = "gone" })));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await admin.CallAsync("admin_delete_user", new { userId = Guid.NewGuid(), confirm = "x" })));
    }

    [Fact]
    public async Task AdminKey_CannotDeleteItself()
    {
        await using var admin = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.AdminKeyToken);
        var me = Ok(await admin.CallAsync("get_me")).GetProperty("user");
        var id = me.GetProperty("id").GetGuid();

        var result = await admin.CallAsync("admin_delete_user", new { userId = id, confirm = AdminTools.DeleteSentinelPrefix + CustomWebApplicationFactory.AdminUserEmail });

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(result));
        Assert.Contains("yourself", McpTestClient.Json(result).GetProperty("message").GetString());
    }

    [Fact]
    public async Task AiSettings_AreMasked_AndTheKeyIsKeptWhenOmitted()
    {
        await using var admin = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.AdminKeyToken);

        var set = Ok(await admin.CallAsync("admin_update_ai_settings", new { apiKey = "sk-test-secret-1234", model = "gpt-4o-mini", enabled = false }));
        Assert.Equal("**************1234", set.GetProperty("apiKeyMasked").GetString());
        Assert.Equal("gpt-4o-mini", set.GetProperty("model").GetString());

        var kept = Ok(await admin.CallAsync("admin_update_ai_settings", new { maxTokens = 512 }));
        Assert.Equal("**************1234", kept.GetProperty("apiKeyMasked").GetString());
        Assert.Equal(512, kept.GetProperty("maxTokens").GetInt32());

        var read = Ok(await admin.CallAsync("admin_get_ai_settings"));
        Assert.Equal("**************1234", read.GetProperty("apiKeyMasked").GetString());
        Assert.DoesNotContain("sk-test-secret", read.GetRawText());

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await admin.CallAsync("admin_update_ai_settings", new { temperature = 5 })));
    }

    [Fact]
    public async Task NonAdminKey_DoesNotSeeAdminTools()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

        var names = (await client.ListToolsAsync()).Select(t => t.Name).ToHashSet();

        Assert.DoesNotContain(names, n => n.StartsWith("admin_"));
        Assert.Contains("get_me", names);
    }
}
