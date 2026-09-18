using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Mcp.Tools;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>Tag tools through the real endpoint: scope enforcement, the lookup, the sentinel and the audit trail.</summary>
public class McpTagToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpTagToolsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private Task<McpClient> WriteClient() => McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

    private async Task<int> CreateTag(McpClient client, string name, int inputTypeId = InputTypeIds.Integer)
    {
        var result = await client.CallAsync("create_tag", new { name, inputTypeId });
        Assert.NotEqual(true, result.IsError);

        return McpTestClient.Json(result).GetProperty("tag").GetProperty("id").GetInt32();
    }

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)];

    // --- Scope ---

    [Fact]
    public async Task ReadOnlyKey_CannotCallAWriteTool()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        // The SDK's authorization filter refuses at the protocol level, before the tool runs.
        var ex = await Assert.ThrowsAsync<McpProtocolException>(() => client.CallAsync("create_tag", new { name = "should not exist", inputTypeId = 1 }));

        Assert.Contains("forbidden", ex.Message, StringComparison.OrdinalIgnoreCase);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        Assert.False(await db.Tags.AnyAsync(t => t.TagName == "should not exist"));
    }

    // --- Create / get / update / list ---

    [Fact]
    public async Task CreateGetUpdate_RoundTrip_WithEncodingHint()
    {
        await using var client = await WriteClient();
        var name = Unique("Water");

        var created = McpTestClient.Json(await client.CallAsync("create_tag",
            new { name, inputTypeId = InputTypeIds.Decimal, isRepeatable = false, maxValue = 5.0, description = "litres" }));

        var id = created.GetProperty("tag").GetProperty("id").GetInt32();
        Assert.Equal(name, created.GetProperty("summary").GetProperty("name").GetString());
        Assert.Equal("Decimal, precision 2", created.GetProperty("summary").GetProperty("inputType").GetString());
        Assert.Contains("two decimals", created.GetProperty("valueEncoding").GetString());
        Assert.False(created.GetProperty("tag").GetProperty("isRepeatable").GetBoolean());
        Assert.Equal(5.0, created.GetProperty("tag").GetProperty("maxValue").GetDouble());

        var fetched = McpTestClient.Json(await client.CallAsync("get_tag", new { tagId = id }));
        Assert.Equal("litres", fetched.GetProperty("tag").GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Null, fetched.GetProperty("options").ValueKind);

        var updated = McpTestClient.Json(await client.CallAsync("update_tag", new { tagId = id, description = "litres per day", isRequired = true }));
        Assert.Equal("litres per day", updated.GetProperty("tag").GetProperty("description").GetString());
        Assert.True(updated.GetProperty("tag").GetProperty("isRequired").GetBoolean());
        // Untouched fields survive the merge.
        Assert.Equal(name, updated.GetProperty("summary").GetProperty("name").GetString());
        Assert.Equal(5.0, updated.GetProperty("tag").GetProperty("maxValue").GetDouble());
        Assert.Equal("Daily", updated.GetProperty("tag").GetProperty("timeGranularity").GetString());
    }

    [Fact]
    public async Task ListTags_FiltersAndPages_WithClampedPageSize()
    {
        await using var client = await WriteClient();
        var stem = Unique("Pager");
        await CreateTag(client, stem + " one");
        await CreateTag(client, stem + " two");

        var page = McpTestClient.Json(await client.CallAsync("list_tags", new { filter = stem, pageSize = 999 }));

        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(PageLimits.MaxPageSize, page.GetProperty("pageSize").GetInt32());
        Assert.All(page.GetProperty("items").EnumerateArray(), item => Assert.Contains(stem, item.GetProperty("title").GetString()));

        var exact = McpTestClient.Json(await client.CallAsync("list_tags", new { filter = stem + " one", filterType = "exact" }));
        Assert.Equal(1, exact.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task FindTag_ResolvesAndReportsCandidates()
    {
        await using var client = await WriteClient();
        var stem = Unique("Find");
        var alpha = await CreateTag(client, stem + " alpha");
        await CreateTag(client, stem + " beta");

        var byId = McpTestClient.Json(await client.CallAsync("find_tag", new { query = alpha.ToString() }));
        Assert.Equal(alpha, byId.GetProperty("match").GetProperty("id").GetInt32());

        var unique = McpTestClient.Json(await client.CallAsync("find_tag", new { query = stem + " al" }));
        Assert.Equal(alpha, unique.GetProperty("match").GetProperty("id").GetInt32());

        var ambiguous = McpTestClient.Json(await client.CallAsync("find_tag", new { query = stem }));
        Assert.Equal(JsonValueKind.Null, ambiguous.GetProperty("match").ValueKind);
        Assert.True(ambiguous.GetProperty("ambiguous").GetBoolean());
        Assert.Equal(2, ambiguous.GetProperty("candidates").GetArrayLength());

        var none = McpTestClient.Json(await client.CallAsync("find_tag", new { query = "no such tag anywhere" }));
        Assert.Equal(JsonValueKind.Null, none.GetProperty("match").ValueKind);
        Assert.Equal(0, none.GetProperty("candidates").GetArrayLength());
    }

    // --- Ownership and references ---

    [Fact]
    public async Task GetTag_OfAnotherUser_IsNotFound()
    {
        int foreignId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            var other = new User { Id = Guid.NewGuid(), Email = $"other-{Guid.NewGuid():N}@example.com", PasswordHash = "x" };
            var tag = new Tag { TagName = "Foreign", UserId = other.Id, InputTypeId = InputTypeIds.Integer, TimeGranularity = TimeGranularity.Daily };
            db.Users.Add(other);
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
            foreignId = tag.Id;
        }

        await using var client = await WriteClient();
        var result = await client.CallAsync("get_tag", new { tagId = foreignId });

        Assert.True(result.IsError);
        Assert.Equal(McpErrorMapper.NotFound, McpTestClient.Json(result).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateTag_WithUnknownGroup_IsNotFound_AndCreatesNothing()
    {
        await using var client = await WriteClient();
        var name = Unique("Orphan");

        var result = await client.CallAsync("create_tag", new { name, inputTypeId = 1, groupId = 999_999 });

        Assert.True(result.IsError);
        Assert.Equal(McpErrorMapper.NotFound, McpTestClient.Json(result).GetProperty("code").GetString());
        var list = McpTestClient.Json(await client.CallAsync("list_tags", new { filter = name, filterType = "exact" }));
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    }

    // --- Sentinel ---

    [Fact]
    public async Task DeleteTag_WithoutSentinel_ReportsImpact_AndDeletesNothing()
    {
        await using var client = await WriteClient();
        var id = await CreateTag(client, Unique("Doomed"));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == CustomWebApplicationFactory.TestUserEmail);
            db.Activities.AddRange(
                new Activity { TagId = id, UserId = user.Id, DateStarted = new DateTime(2026, 1, 1), Description = "1" },
                new Activity { TagId = id, UserId = user.Id, DateStarted = new DateTime(2026, 1, 2), Description = "2" },
                new Activity { TagId = id, UserId = user.Id, DateStarted = new DateTime(2026, 1, 3), Description = "3" });
            await db.SaveChangesAsync();
        }

        try
        {
            var refused = await client.CallAsync("delete_tag", new { tagId = id });
            Assert.True(refused.IsError);
            var error = McpTestClient.Json(refused);
            Assert.Equal(McpErrorMapper.ConfirmationRequired, error.GetProperty("code").GetString());
            Assert.Equal(TagTools.DeleteSentinelPrefix + id, error.GetProperty("expected").GetString());
            Assert.Contains("3 activities", error.GetProperty("impact").GetString());

            var wrong = await client.CallAsync("delete_tag", new { tagId = id, confirm = "DELETE_TAG_0" });
            Assert.True(wrong.IsError);

            Assert.NotEqual(true, (await client.CallAsync("get_tag", new { tagId = id })).IsError);
        }
        finally
        {
            // The in-memory database is shared with tests that count the user's activities.
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            db.Activities.RemoveRange(db.Activities.Where(a => a.TagId == id));
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task DeleteTag_WithSentinel_Deletes_AndIsAudited()
    {
        await using var client = await WriteClient();
        var id = await CreateTag(client, Unique("Gone"));

        var result = McpTestClient.Json(await client.CallAsync("delete_tag", new { tagId = id, confirm = TagTools.DeleteSentinelPrefix + id }));

        Assert.True(result.GetProperty("deleted").GetBoolean());
        Assert.Equal(0, result.GetProperty("activitiesDeleted").GetInt32());
        var gone = await client.CallAsync("get_tag", new { tagId = id });
        Assert.True(gone.IsError);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == CustomWebApplicationFactory.TestUserEmail);

        // The arguments travel in the detail row, sentinel included.
        var rows = await db.EventLogs.Include(e => e.Detail)
            .Where(e => e.UserId == user.Id && e.Message.StartsWith("MCP delete_tag via lmd_") && e.Message.EndsWith(": ok"))
            .ToListAsync();
        var row = Assert.Single(rows, e => e.Detail != null && e.Detail.Description.Contains($"\"tagId\":{id}"));
        Assert.Contains(TagTools.DeleteSentinelPrefix + id, row.Detail!.Description);

        // And the Event Log page's MCP category isolates the audit trail.
        var audit = await scope.ServiceProvider.GetRequiredService<IEventLogService>()
            .GetPaged(1, 200, user.Id, isAdmin: false, categoryFilter: EventLogCategoryFilter.Mcp);
        Assert.Contains(audit.Items, e => e.Message.StartsWith("MCP create_tag via lmd_"));
        Assert.Contains(audit.Items, e => e.Message.StartsWith("MCP delete_tag via lmd_"));
        Assert.All(audit.Items, e => Assert.StartsWith("MCP ", e.Message));
    }

    [Fact]
    public async Task FailedWriteTool_IsAuditedAsError()
    {
        await using var client = await WriteClient();

        await client.CallAsync("update_tag", new { tagId = 999_999, name = "nope" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        Assert.True(await db.EventLogs.AnyAsync(e => e.Message.StartsWith("MCP update_tag via lmd_") && e.Message.EndsWith(": error") && e.Level == EventLogLevel.Error));
    }
}
