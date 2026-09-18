using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Mcp.Infrastructure;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>get_activity_summary through the real endpoint over data logged with log_value.</summary>
public class McpAnalyticsToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpAnalyticsToolsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static JsonElement Ok(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);

        return McpTestClient.Json(result);
    }

    [Fact]
    public async Task NumericTag_TotalsBucketsAndStreak()
    {
        await using var write = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var name = Unique("Steps");
        Ok(await write.CallAsync("create_tag", new { name, inputTypeId = InputTypeIds.Integer }));
        foreach (var (day, value) in new[] { ("2026-07-01", 1000), ("2026-07-02", 3000), ("2026-07-03", 2000), ("2026-07-05", 4000) })
        {
            Ok(await write.CallAsync("log_value", new { tag = name, value, dateTime = day + "T18:00" }));
        }

        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);
        var s = Ok(await client.CallAsync("get_activity_summary", new { tag = name, from = "2026-07-01", to = "2026-07-05" }));

        Assert.Equal("numeric", s.GetProperty("valueKind").GetString());
        Assert.Equal("stored-local-date", s.GetProperty("convention").GetString());
        Assert.Equal(4, s.GetProperty("count").GetInt32());
        Assert.Equal(10000, s.GetProperty("sum").GetDouble());
        Assert.Equal(1000, s.GetProperty("min").GetDouble());
        Assert.Equal(4000, s.GetProperty("max").GetDouble());
        Assert.Equal(2500, s.GetProperty("average").GetDouble());
        Assert.Equal(4, s.GetProperty("daysWithData").GetInt32());
        Assert.Equal(5, s.GetProperty("daysInRange").GetInt32());
        Assert.Equal(3, s.GetProperty("longestStreak").GetInt32());
        Assert.Equal("2026-07-01", s.GetProperty("longestStreakStart").GetString());
        Assert.Equal(1, s.GetProperty("currentStreak").GetInt32());
        Assert.Equal("Day", s.GetProperty("bucket").GetString());
        var buckets = s.GetProperty("buckets").EnumerateArray().ToList();
        Assert.Equal(5, buckets.Count);
        Assert.Equal(0, buckets[3].GetProperty("count").GetInt32());
        Assert.Equal(4000, buckets[4].GetProperty("sum").GetDouble());
    }

    [Fact]
    public async Task BooleanTag_TrueShare_AndWeekBucket()
    {
        await using var write = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var name = Unique("Ran");
        Ok(await write.CallAsync("create_tag", new { name, inputTypeId = InputTypeIds.Boolean }));
        foreach (var (day, value) in new[] { ("2026-07-06", "yes"), ("2026-07-07", "no"), ("2026-07-08", "yes"), ("2026-07-09", "yes") })
        {
            Ok(await write.CallAsync("log_value", new { tag = name, value, dateTime = day + "T07:00" }));
        }

        // en-US weeks start on Sunday; ending on Saturday the 11th keeps it to one bucket.
        var s = Ok(await write.CallAsync("get_activity_summary", new { tag = name, from = "2026-07-06", to = "2026-07-11", bucket = "Week" }));

        Assert.Equal("boolean", s.GetProperty("valueKind").GetString());
        Assert.Equal(3, s.GetProperty("sum").GetDouble());
        Assert.Equal(0.75, s.GetProperty("average").GetDouble());
        Assert.Equal(JsonValueKind.Null, s.GetProperty("min").ValueKind);
        Assert.Equal(3, s.GetProperty("daysWithData").GetInt32());
        Assert.Single(s.GetProperty("buckets").EnumerateArray());
    }

    [Fact]
    public async Task Defaults_ToTheLastThirtyDays_AndErrorsSurfaceWithHints()
    {
        await using var write = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var name = Unique("Mood");
        Ok(await write.CallAsync("create_tag", new { name, inputTypeId = InputTypeIds.String }));

        var s = Ok(await write.CallAsync("get_activity_summary", new { tag = name }));
        Assert.Equal(30, s.GetProperty("daysInRange").GetInt32());
        Assert.Equal("text", s.GetProperty("valueKind").GetString());

        var tooLong = await write.CallAsync("get_activity_summary", new { tag = name, from = "2024-01-01", to = "2026-01-01" });
        Assert.True(tooLong.IsError);
        var error = McpTestClient.Json(tooLong);
        Assert.Equal(McpErrorMapper.Invalid, error.GetProperty("code").GetString());
        Assert.Contains("Week", error.GetProperty("message").GetString());

        var unknown = await write.CallAsync("get_activity_summary", new { tag = "no such tag at all" });
        Assert.Equal(McpErrorMapper.NotFound, McpTestClient.Json(unknown).GetProperty("code").GetString());
    }
}
