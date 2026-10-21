using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Mcp.Infrastructure;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>Record tools through the real endpoint: a record type with its fields, a record logged
/// by field name, and the refusal of fields and record types in the single-value tools.</summary>
public class McpRecordToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpRecordToolsTests(CustomWebApplicationFactory factory)
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
    public async Task CreateRecordType_LogRecordByFieldName_AndReadItBack()
    {
        await using var client = await WriteClient();
        var name = Unique("Workout");

        var type = Ok(await client.CallAsync("create_record_type", new
        {
            name,
            fields = new object[]
            {
                new { name = "Duration", inputTypeId = InputTypeIds.Integer },
                new { name = "Distance", inputTypeId = InputTypeIds.Decimal, minValue = 0.0 },
                new { name = "Effort", inputTypeId = InputTypeIds.StarRating },
                new { name = "Warm-up done", inputTypeId = InputTypeIds.Boolean },
                new { name = "Note", inputTypeId = InputTypeIds.String }
            }
        }));
        var typeId = type.GetProperty("id").GetInt32();
        Assert.Equal(["Duration", "Distance", "Effort", "Warm-up done", "Note"],
            type.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("name").GetString()));
        Assert.Equal($"{name}: Distance", type.GetProperty("fields")[1].GetProperty("tagName").GetString());

        var listed = Ok(await client.CallAsync("list_record_types"));
        Assert.Contains(listed.EnumerateArray(), t => t.GetProperty("id").GetInt32() == typeId);

        var record = Ok(await client.CallAsync("log_record", new
        {
            recordType = name,
            dateTime = "2026-10-20T07:30",
            values = new object[]
            {
                new { field = "Duration", value = (object)31 },
                new { field = "distance", value = (object)5.2 },
                new { field = "Effort", value = (object)4 },
                new { field = "Warm-up done", value = (object)true },
                new { field = "Note", value = (object?)null }
            }
        }));
        Assert.Equal(["31", "5.20", "4", "true"], record.GetProperty("values").EnumerateArray().Select(v => v.GetProperty("value").GetString()));
        Assert.Equal(new DateTime(2026, 10, 20, 7, 30, 0), record.GetProperty("dateStarted").GetDateTime());

        var fetched = Ok(await client.CallAsync("get_record", new { recordId = record.GetProperty("id").GetInt32() }));
        Assert.Equal(name, fetched.GetProperty("recordTypeName").GetString());
        Assert.Equal(4, fetched.GetProperty("values").GetArrayLength());

        // By id too, and the record type now counts it.
        Ok(await client.CallAsync("log_record", new { recordType = typeId.ToString(), values = new object[] { new { field = "Effort", value = (object)2 } } }));
        var again = Ok(await client.CallAsync("list_record_types")).EnumerateArray().Single(t => t.GetProperty("id").GetInt32() == typeId);
        Assert.Equal(2, again.GetProperty("recordCount").GetInt32());
    }

    [Fact]
    public async Task LogRecord_RefusesAnUnknownField_AndARecordWithoutValues()
    {
        await using var client = await WriteClient();
        var name = Unique("Pressure");
        Ok(await client.CallAsync("create_record_type", new { name, fields = new object[] { new { name = "Systolic", inputTypeId = InputTypeIds.Integer } } }));

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("log_record",
            new { recordType = name, values = new object[] { new { field = "Pulse", value = (object)60 } } })));
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("log_record",
            new { recordType = name, values = new object[] { new { field = "Systolic", value = (object?)null } } })));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("log_record",
            new { recordType = Unique("Nothing"), values = new object[] { new { field = "Systolic", value = (object)120 } } })));
    }

    [Fact]
    public async Task AFieldOrARecordType_IsNotLoggedWithLogValue()
    {
        await using var client = await WriteClient();
        var name = Unique("Pulse check");
        var type = Ok(await client.CallAsync("create_record_type", new { name, fields = new object[] { new { name = "Pulse", inputTypeId = InputTypeIds.Integer } } }));
        var fieldTagId = type.GetProperty("fields")[0].GetProperty("tagId").GetInt32();

        var refused = await client.CallAsync("log_value", new { tag = fieldTagId.ToString(), value = 60 });
        Assert.Equal(McpErrorMapper.Conflict, ErrorCode(refused));
        Assert.Contains("log_record", McpTestClient.Json(refused).GetProperty("hint").GetString());

        // The record type is not a tag the lookup offers.
        var typeId = type.GetProperty("id").GetInt32();
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("log_value", new { tag = typeId.ToString(), value = 1 })));
    }

    [Fact]
    public async Task ReadOnlyKey_CannotCreateARecordType()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var ex = await Assert.ThrowsAsync<McpProtocolException>(() => client.CallAsync("create_record_type",
            new { name = "should not exist", fields = new object[] { new { name = "A", inputTypeId = InputTypeIds.Integer } } }));

        Assert.Contains("forbidden", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
