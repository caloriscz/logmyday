using System.Text.Json;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>Groups, option lists, units, colour schemes and input types through the real endpoint.</summary>
public class McpReferenceDataToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpReferenceDataToolsTests(CustomWebApplicationFactory factory)
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

    // --- Groups ---

    [Fact]
    public async Task TagGroups_RoundTrip_AndDeleteDetachesTags()
    {
        await using var client = await WriteClient();
        var groupName = Unique("Health");

        var group = Ok(await client.CallAsync("create_tag_group", new { name = groupName, description = "body stuff" }));
        var groupId = group.GetProperty("id").GetInt32();
        Assert.Contains(Ok(await client.CallAsync("list_tag_groups")).EnumerateArray(), g => g.GetProperty("id").GetInt32() == groupId);

        var renamed = Ok(await client.CallAsync("update_tag_group", new { groupId, name = groupName + " 2" }));
        Assert.Equal(groupName + " 2", renamed.GetProperty("name").GetString());
        Assert.Equal("body stuff", renamed.GetProperty("description").GetString());

        var tagName = Unique("Iron");
        var tag = Ok(await client.CallAsync("create_tag", new { name = tagName, inputTypeId = InputTypeIds.Integer, groupId }));
        var tagId = tag.GetProperty("tag").GetProperty("id").GetInt32();
        Assert.Equal($"{groupName} 2: {tagName}", tag.GetProperty("summary").GetProperty("title").GetString());

        // The grouped syntax resolves it; the ungrouped syntax does not.
        Assert.Equal(tagId, Ok(await client.CallAsync("find_tag", new { query = $"{groupName} 2:{tagName}" })).GetProperty("match").GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, Ok(await client.CallAsync("find_tag", new { query = ":" + tagName })).GetProperty("match").ValueKind);

        Ok(await client.CallAsync("delete_tag_group", new { groupId }));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("get_tag_group", new { groupId })));
        var detached = Ok(await client.CallAsync("get_tag", new { tagId }));
        Assert.Equal(JsonValueKind.Null, detached.GetProperty("tag").GetProperty("groupId").ValueKind);
    }

    // --- Option lists ---

    [Fact]
    public async Task OptionLists_DriveValueEncoding_ReplaceOptions_AndRefuseDeleteWhileInUse()
    {
        await using var client = await WriteClient();

        var list = Ok(await client.CallAsync("create_option_list", new
        {
            name = Unique("Mood"),
            options = new object[] { new { value = "happy", displayName = "Happy 😊" }, new { value = "meh" }, new { value = "sad" } }
        }));
        var listId = list.GetProperty("id").GetInt32();
        Assert.False(list.GetProperty("isGlobal").GetBoolean());
        Assert.Equal(3, list.GetProperty("options").GetArrayLength());

        var tagName = Unique("Feeling");
        var tag = Ok(await client.CallAsync("create_tag", new { name = tagName, inputTypeId = InputTypeIds.String, optionListId = listId }));
        var tagId = tag.GetProperty("tag").GetProperty("id").GetInt32();
        Assert.Equal(3, tag.GetProperty("options").GetArrayLength());

        // The display name is accepted and the stored value is the option's value.
        var logged = Ok(await client.CallAsync("log_value", new { tag = tagName, value = "Happy 😊", dateTime = "2026-06-01T09:00" }));
        Assert.Equal("happy", logged.GetProperty("storedValue").GetString());
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("log_value", new { tag = tagName, value = "angry" })));

        // Replace: keep "happy" by id, drop the rest, add "great".
        var happyId = list.GetProperty("options").EnumerateArray().First(o => o.GetProperty("value").GetString() == "happy").GetProperty("id").GetInt32();
        var updated = Ok(await client.CallAsync("update_option_list", new
        {
            optionListId = listId,
            options = new object[] { new { id = happyId, value = "happy", displayName = "Happy" }, new { value = "great" } }
        }));
        Assert.Equal(["happy", "great"], updated.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("value").GetString()).OrderByDescending(v => v == "happy"));
        Assert.Equal(happyId, updated.GetProperty("options").EnumerateArray().First(o => o.GetProperty("value").GetString() == "happy").GetProperty("id").GetInt32());

        var inUse = await client.CallAsync("delete_option_list", new { optionListId = listId });
        Assert.Equal(McpErrorMapper.Conflict, ErrorCode(inUse));

        Ok(await client.CallAsync("update_tag", new { tagId, optionListId = 0 }));
        Ok(await client.CallAsync("delete_option_list", new { optionListId = listId }));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("get_option_list", new { optionListId = listId })));
    }

    [Fact]
    public async Task OptionLists_GlobalNeedsAnAdmin_AndEmptyIsInvalid()
    {
        await using var client = await WriteClient();

        var forbidden = await client.CallAsync("create_option_list", new { name = Unique("Global"), isGlobal = true, options = new[] { new { value = "a" } } });
        Assert.Equal(McpErrorMapper.Forbidden, ErrorCode(forbidden));

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("create_option_list", new { name = Unique("Empty"), options = Array.Empty<object>() })));
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("create_option_list", new { name = Unique("Dup"), options = new[] { new { value = "a" }, new { value = "A" } } })));
    }

    // --- Units ---

    [Fact]
    public async Task Units_RoundTrip_SentinelAndInUseConflict()
    {
        int quantityId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            var quantity = new Quantity { Key = Unique("mass") };
            db.Set<Quantity>().Add(quantity);
            await db.SaveChangesAsync();
            quantityId = quantity.Id;
        }

        await using var client = await WriteClient();
        Assert.Contains(Ok(await client.CallAsync("list_quantities")).EnumerateArray(), q => q.GetProperty("id").GetInt32() == quantityId);

        var key = Unique("mg");
        var unit = Ok(await client.CallAsync("create_unit", new { key, symbol = "mg", quantityId, aToBase = 0.001, decimals = 1 }));
        var unitId = unit.GetProperty("id").GetInt32();
        Assert.Equal(0.001, unit.GetProperty("aToBase").GetDouble());
        Assert.Equal(0, unit.GetProperty("bToBase").GetDouble());

        var updated = Ok(await client.CallAsync("update_unit", new { unitId, symbol = "mg." }));
        Assert.Equal("mg.", updated.GetProperty("symbol").GetString());
        Assert.Equal(key, updated.GetProperty("key").GetString());
        Assert.Contains(Ok(await client.CallAsync("list_units")).EnumerateArray(), u => u.GetProperty("id").GetInt32() == unitId);

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("create_unit", new { key = Unique("bad"), symbol = "x", quantityId, aToBase = 0 })));

        var tag = Ok(await client.CallAsync("create_tag", new { name = Unique("Dose"), inputTypeId = InputTypeIds.Integer, unitId }));
        var tagId = tag.GetProperty("tag").GetProperty("id").GetInt32();
        Assert.Equal("mg.", tag.GetProperty("summary").GetProperty("unitSymbol").GetString());

        var refused = await client.CallAsync("delete_unit", new { unitId });
        Assert.Equal(McpErrorMapper.ConfirmationRequired, ErrorCode(refused));
        Assert.Equal(UnitTools.DeleteSentinelPrefix + unitId, McpTestClient.Json(refused).GetProperty("expected").GetString());
        Assert.Contains("every user", McpTestClient.Json(refused).GetProperty("impact").GetString());

        var inUse = await client.CallAsync("delete_unit", new { unitId, confirm = UnitTools.DeleteSentinelPrefix + unitId });
        Assert.Equal(McpErrorMapper.Conflict, ErrorCode(inUse));
        Assert.Contains("in use", McpTestClient.Json(inUse).GetProperty("message").GetString());

        Ok(await client.CallAsync("update_tag", new { tagId, unitId = 0 }));
        Ok(await client.CallAsync("delete_unit", new { unitId, confirm = UnitTools.DeleteSentinelPrefix + unitId }));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("get_unit", new { unitId })));
    }

    // --- Colour schemes ---

    [Fact]
    public async Task ColorSchemes_ExactAndRangeEntries_ReplaceAndDelete()
    {
        await using var client = await WriteClient();

        var scheme = Ok(await client.CallAsync("create_color_scheme", new
        {
            name = Unique("YesNo"),
            entries = new object[] { new { color = "#16a34a", exactValue = 1, label = "Yes" }, new { color = "#dc2626", exactValue = 0, label = "No" } }
        }));
        var schemeId = scheme.GetProperty("id").GetInt32();
        var yes = scheme.GetProperty("entries").EnumerateArray().First(e => e.GetProperty("label").GetString() == "Yes");
        Assert.Equal(1, yes.GetProperty("rangeFrom").GetDouble());
        Assert.Equal(1, yes.GetProperty("rangeTo").GetDouble());
        Assert.Equal(0, yes.GetProperty("sortOrder").GetInt32());

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("create_color_scheme", new
        {
            name = Unique("Bad"),
            entries = new[] { new { color = "#000", exactValue = 1, rangeTo = 5 } }
        })));

        var replaced = Ok(await client.CallAsync("update_color_scheme", new
        {
            colorSchemeId = schemeId,
            entries = new object[] { new { color = "#fff", rangeTo = 3, label = "low" }, new { color = "#000", rangeFrom = 4, label = "high", sortOrder = 9 } }
        }));
        var entries = replaced.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        var low = entries.First(e => e.GetProperty("label").GetString() == "low");
        Assert.Equal(JsonValueKind.Null, low.GetProperty("rangeFrom").ValueKind);
        Assert.Equal(3, low.GetProperty("rangeTo").GetDouble());
        Assert.Equal(9, entries.First(e => e.GetProperty("label").GetString() == "high").GetProperty("sortOrder").GetInt32());

        var tag = Ok(await client.CallAsync("create_tag", new { name = Unique("Coloured"), inputTypeId = InputTypeIds.Integer, colorSchemeId = schemeId }));
        var tagId = tag.GetProperty("tag").GetProperty("id").GetInt32();
        Assert.Equal(schemeId, tag.GetProperty("tag").GetProperty("colorSchemeId").GetInt32());

        Ok(await client.CallAsync("delete_color_scheme", new { colorSchemeId = schemeId }));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("get_color_scheme", new { colorSchemeId = schemeId })));
        Assert.DoesNotContain(schemeId, Ok(await client.CallAsync("list_color_schemes")).EnumerateArray().Select(s => s.GetProperty("id").GetInt32()));
        // The tag's colorSchemeId falls back to null through the SetNull FK — a database behaviour the InMemory provider does not reproduce.
    }

    // --- Input types ---

    [Fact]
    public async Task InputTypes_ListAllElevenWithEncodingRules()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var types = Ok(await client.CallAsync("list_input_types")).EnumerateArray().ToList();

        Assert.Equal(11, types.Count);
        Assert.All(types, t => Assert.False(string.IsNullOrEmpty(t.GetProperty("valueEncoding").GetString())));
        Assert.Contains(types, t => t.GetProperty("id").GetInt32() == InputTypeIds.Boolean && t.GetProperty("valueEncoding").GetString()!.Contains("yes/no"));
    }
}
