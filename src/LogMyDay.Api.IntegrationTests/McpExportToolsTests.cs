using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>Excel export tools through the real endpoint, including the embedded workbook blob.</summary>
public class McpExportToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpExportToolsTests(CustomWebApplicationFactory factory)
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
    public async Task PreviewOldestAndGenerate_ForOneTag()
    {
        await using var write = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var name = Unique("Sheet");
        var tagId = Ok(await write.CallAsync("create_tag", new { name, inputTypeId = InputTypeIds.Integer })).GetProperty("tag").GetProperty("id").GetInt32();
        Ok(await write.CallAsync("log_value", new { tag = name, value = 5, dateTime = "2026-02-10T09:00" }));
        Ok(await write.CallAsync("log_value", new { tag = name, value = 6, dateTime = "2026-02-12T09:00" }));

        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var preview = Ok(await client.CallAsync("preview_export", new { tagIds = new[] { tagId }, from = "2026-02-01", to = "2026-02-28" }));
        Assert.Equal(2, preview.GetProperty("totalActivities").GetInt32());
        Assert.Equal(1, preview.GetProperty("selectedTags").GetInt32());

        var oldest = Ok(await client.CallAsync("get_oldest_activity_date", new { tagIds = new[] { tagId } }));
        Assert.Equal(new DateTime(2026, 2, 10, 9, 0, 0), oldest.GetProperty("oldestActivityDate").GetDateTime());

        var result = await client.CallAsync("generate_export", new { tagIds = new[] { tagId }, from = "2026-02-01", to = "2026-02-28", freezeFirstRow = true });
        Assert.NotEqual(true, result.IsError);
        var text = Assert.Single(result.Content.OfType<TextContentBlock>());
        var summary = JsonDocument.Parse(text.Text).RootElement;
        Assert.EndsWith(".xlsx", summary.GetProperty("fileName").GetString());
        Assert.Equal(2, summary.GetProperty("statistics").GetProperty("totalActivities").GetInt32());

        var blob = Assert.IsType<BlobResourceContents>(Assert.Single(result.Content.OfType<EmbeddedResourceBlock>()).Resource);
        Assert.Equal(ExportTools.XlsxMime, blob.MimeType);
        var bytes = blob.DecodedData.ToArray();
        Assert.Equal(summary.GetProperty("bytes").GetInt32(), bytes.Length);
        // An xlsx is a zip: "PK" magic.
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task Export_RefusesForeignTags_EmptySelection_AndReversedRange()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var tagId = Ok(await client.CallAsync("create_tag", new { name = Unique("Own"), inputTypeId = InputTypeIds.Integer })).GetProperty("tag").GetProperty("id").GetInt32();

        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("preview_export", new { tagIds = new[] { tagId, 999_999 } })));
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("preview_export", new { tagIds = Array.Empty<int>() })));
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("generate_export", new { tagIds = new[] { tagId }, from = "2026-03-01", to = "2026-02-01" })));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("get_oldest_activity_date", new { tagIds = new[] { 999_999 } })));
    }
}
