using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Mcp.Resources;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>The logmyday:// resources through the real endpoint, readable with a read-only key.</summary>
public class McpResourcesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpResourcesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static JsonElement Read(ReadResourceResult result, string expectedUri)
    {
        var content = Assert.Single(result.Contents);
        var text = Assert.IsType<TextResourceContents>(content);
        Assert.Equal(expectedUri, text.Uri);
        Assert.Equal(LogMyDayResources.JsonMime, text.MimeType);

        return JsonDocument.Parse(text.Text).RootElement;
    }

    [Fact]
    public async Task ResourcesList_ExposesTheSixDirectResources_AndTheTagTemplate()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var direct = (await client.ListResourcesAsync()).Select(r => r.Uri).OrderBy(u => u).ToList();
        var templates = (await client.ListResourceTemplatesAsync()).Select(t => t.UriTemplate).ToList();

        Assert.Equal(
            ["logmyday://input-types", "logmyday://me", "logmyday://option-lists", "logmyday://tag-groups", "logmyday://tags", "logmyday://units"],
            direct);
        Assert.Equal(["logmyday://tags/{id}"], templates);
    }

    [Fact]
    public async Task ReadOnlyKey_CanReadEveryResource()
    {
        await using var write = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var name = $"Res {Guid.NewGuid():N}"[..12];
        var created = McpTestClient.Json(await write.CallAsync("create_tag", new { name, inputTypeId = InputTypeIds.Boolean }));
        var tagId = created.GetProperty("tag").GetProperty("id").GetInt32();

        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var tags = Read(await client.ReadResourceAsync("logmyday://tags"), "logmyday://tags");
        Assert.Contains(tags.EnumerateArray(), t => t.GetProperty("id").GetInt32() == tagId && t.GetProperty("inputType").GetString() == "Boolean");

        var tag = Read(await client.ReadResourceAsync($"logmyday://tags/{tagId}"), $"logmyday://tags/{tagId}");
        Assert.Equal(name, tag.GetProperty("tag").GetProperty("title").GetString());
        Assert.Contains("yes/no", tag.GetProperty("valueEncoding").GetString());

        Assert.Equal(JsonValueKind.Array, Read(await client.ReadResourceAsync("logmyday://tag-groups"), "logmyday://tag-groups").ValueKind);
        Assert.Equal(JsonValueKind.Array, Read(await client.ReadResourceAsync("logmyday://option-lists"), "logmyday://option-lists").ValueKind);

        var units = Read(await client.ReadResourceAsync("logmyday://units"), "logmyday://units");
        Assert.Equal(JsonValueKind.Array, units.GetProperty("units").ValueKind);
        Assert.Equal(JsonValueKind.Array, units.GetProperty("quantities").ValueKind);

        var inputTypes = Read(await client.ReadResourceAsync("logmyday://input-types"), "logmyday://input-types");
        Assert.Equal(11, inputTypes.GetArrayLength());

        var me = Read(await client.ReadResourceAsync("logmyday://me"), "logmyday://me");
        Assert.Equal(CustomWebApplicationFactory.McpUserEmail, me.GetProperty("email").GetString());
        Assert.False(me.GetProperty("isAdmin").GetBoolean());
        Assert.False(string.IsNullOrEmpty(me.GetProperty("timeZone").GetString()));
    }

    [Fact]
    public async Task UnknownTag_AndUnknownUri_AreErrors()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        await Assert.ThrowsAnyAsync<McpException>(() => client.ReadResourceAsync("logmyday://tags/999999").AsTask());
        await Assert.ThrowsAnyAsync<McpException>(() => client.ReadResourceAsync("logmyday://nothing").AsTask());
    }
}
