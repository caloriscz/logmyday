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
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// Activity tools through the real endpoint. Each test creates its own tags so the shared in-memory
/// database's seeded rows (which other tests count) stay untouched.
/// </summary>
public class McpActivityToolsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpActivityToolsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private Task<McpClient> WriteClient() => McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static async Task<(int Id, string Name)> CreateTag(McpClient client, string prefix, int inputTypeId, bool isRepeatable = true, double? maxValue = null,
        TimeGranularity timeGranularity = TimeGranularity.Daily)
    {
        var name = Unique(prefix);
        var result = await client.CallAsync("create_tag", new { name, inputTypeId, isRepeatable, maxValue, timeGranularity });
        Assert.NotEqual(true, result.IsError);

        return (McpTestClient.Json(result).GetProperty("tag").GetProperty("id").GetInt32(), name);
    }

    private static string ErrorCode(CallToolResult result)
    {
        Assert.True(result.IsError, "expected an error result");

        return McpTestClient.Json(result).GetProperty("code").GetString()!;
    }

    // --- log_value ---

    [Fact]
    public async Task LogValue_Twice_OnNonRepeatableInteger_Accumulates()
    {
        await using var client = await WriteClient();
        var (id, name) = await CreateTag(client, "VitD", InputTypeIds.Integer, isRepeatable: false);

        var first = McpTestClient.Json(await client.CallAsync("log_value", new { tag = name, value = 2000, dateTime = "2026-03-10T08:00" }));
        Assert.Equal(ActivityTools.ActionCreated, first.GetProperty("action").GetString());
        Assert.Equal("2000", first.GetProperty("storedValue").GetString());
        Assert.Equal(id, first.GetProperty("activity").GetProperty("tagId").GetInt32());

        var second = McpTestClient.Json(await client.CallAsync("log_value", new { tag = id.ToString(), value = "1000", dateTime = "2026-03-10T20:00" }));
        Assert.Equal(ActivityTools.ActionAccumulated, second.GetProperty("action").GetString());
        Assert.Equal("3000", second.GetProperty("storedValue").GetString());
        Assert.Equal(first.GetProperty("activity").GetProperty("id").GetInt32(), second.GetProperty("activity").GetProperty("id").GetInt32());

        // A different day starts a new row.
        var nextDay = McpTestClient.Json(await client.CallAsync("log_value", new { tag = name, value = 500, dateTime = "2026-03-11T08:00" }));
        Assert.Equal(ActivityTools.ActionCreated, nextDay.GetProperty("action").GetString());
    }

    [Fact]
    public async Task LogValue_SetMode_ReplacesThePeriodValue_AndIsRefusedForRepeatable()
    {
        await using var client = await WriteClient();
        var (_, name) = await CreateTag(client, "Weight", InputTypeIds.Decimal, isRepeatable: false);

        await client.CallAsync("log_value", new { tag = name, value = 80.4, dateTime = "2026-03-12T07:00" });
        var replaced = McpTestClient.Json(await client.CallAsync("log_value", new { tag = name, value = 79.9, dateTime = "2026-03-12T21:00", mode = "set" }));
        Assert.Equal(ActivityTools.ActionReplaced, replaced.GetProperty("action").GetString());
        Assert.Equal("79.90", replaced.GetProperty("storedValue").GetString());

        var (_, repeatable) = await CreateTag(client, "Steps", InputTypeIds.Integer);
        var refused = await client.CallAsync("log_value", new { tag = repeatable, value = 10, mode = "set" });
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(refused));
        Assert.Contains("repeatable", McpTestClient.Json(refused).GetProperty("message").GetString());
    }

    [Fact]
    public async Task LogValue_MaxValueOverflow_IsInvalid_WithTheServiceMessage()
    {
        await using var client = await WriteClient();
        var (_, name) = await CreateTag(client, "Coffee", InputTypeIds.Integer, isRepeatable: false, maxValue: 3);

        await client.CallAsync("log_value", new { tag = name, value = 2, dateTime = "2026-03-13T09:00" });
        var overflow = await client.CallAsync("log_value", new { tag = name, value = 2, dateTime = "2026-03-13T15:00" });

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(overflow));
        Assert.Contains("exceeds the maximum 3", McpTestClient.Json(overflow).GetProperty("message").GetString());
    }

    [Fact]
    public async Task LogValue_OnALockedDay_IsTagDayLocked_AndLogsNothing()
    {
        await using var client = await WriteClient();
        var (id, name) = await CreateTag(client, "Locked", InputTypeIds.Integer);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == CustomWebApplicationFactory.McpUserEmail);
            await scope.ServiceProvider.GetRequiredService<ITagDayLockService>()
                .Upsert(user.Id, new TagDayLockRequest { TagId = id, Date = new DateOnly(2026, 3, 14), IsLocked = true }, DayLockSetBy.User);
        }

        var result = await client.CallAsync("log_value", new { tag = name, value = 1, dateTime = "2026-03-14T12:00" });

        Assert.Equal(McpErrorMapper.TagDayLocked, ErrorCode(result));
        var error = McpTestClient.Json(result);
        Assert.Equal(id, error.GetProperty("tagId").GetInt32());
        Assert.Equal("2026-03-14", error.GetProperty("date").GetString());
        Assert.Contains("set_tag_day_lock", error.GetProperty("hint").GetString());

        var rows = McpTestClient.Json(await client.CallAsync("list_activities", new { tagId = id }));
        Assert.Equal(0, rows.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task LogValue_EncodesForTheInputType_AndDefaultsToNowInTheUsersZone()
    {
        await using var client = await WriteClient();
        var (_, name) = await CreateTag(client, "Ran", InputTypeIds.Boolean);

        var logged = McpTestClient.Json(await client.CallAsync("log_value", new { tag = name, value = "yes" }));

        Assert.Equal("true", logged.GetProperty("storedValue").GetString());
        var started = logged.GetProperty("activity").GetProperty("dateStarted").GetDateTime();
        var vienna = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna"));
        Assert.True(Math.Abs((started - vienna).TotalMinutes) < 5, $"expected ~{vienna:s} in the user's zone, got {started:s}");
    }

    [Fact]
    public async Task LogValue_UnknownOrAmbiguousTag_SaysSo()
    {
        await using var client = await WriteClient();
        var stem = Unique("Amb");
        await CreateTag(client, stem + " x", InputTypeIds.Integer);
        await CreateTag(client, stem + " y", InputTypeIds.Integer);

        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("log_value", new { tag = "definitely not a tag", value = 1 })));

        var ambiguous = await client.CallAsync("log_value", new { tag = stem, value = 1 });
        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(ambiguous));
        Assert.Contains("ambiguous", McpTestClient.Json(ambiguous).GetProperty("message").GetString());
    }

    [Fact]
    public async Task LogValue_BadValueForTheType_IsInvalid_BeforeAnythingIsWritten()
    {
        await using var client = await WriteClient();
        var (id, name) = await CreateTag(client, "Stars", InputTypeIds.StarRating);

        Assert.Equal(McpErrorMapper.Invalid, ErrorCode(await client.CallAsync("log_value", new { tag = name, value = 9 })));
        Assert.Equal(0, McpTestClient.Json(await client.CallAsync("list_activities", new { tagId = id })).GetProperty("totalCount").GetInt32());
    }

    // --- create / update / delete / get ---

    [Fact]
    public async Task CreateActivity_WithAnotherUsersTag_IsNotFound()
    {
        int foreignId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            var other = new User { Id = Guid.NewGuid(), Email = $"other-{Guid.NewGuid():N}@example.com", PasswordHash = "x" };
            var tag = new Tag { TagName = "Theirs", UserId = other.Id, InputTypeId = InputTypeIds.Integer, TimeGranularity = TimeGranularity.Daily };
            db.Users.Add(other);
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
            foreignId = tag.Id;
        }

        await using var client = await WriteClient();
        var result = await client.CallAsync("create_activity", new { tagId = foreignId, value = 1, dateTime = "2026-03-15T10:00" });

        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(result));
        using var check = _factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        Assert.False(await db2.Activities.AnyAsync(a => a.TagId == foreignId));
    }

    [Fact]
    public async Task CreateUpdateGetDelete_RoundTrip()
    {
        await using var client = await WriteClient();
        var (id, _) = await CreateTag(client, "Note", InputTypeIds.String);

        var created = McpTestClient.Json(await client.CallAsync("create_activity", new { tagId = id, value = "first draft", dateTime = "2026-03-16T09:30" }));
        Assert.False(created.GetProperty("wasAccumulated").GetBoolean());
        var activityId = created.GetProperty("activity").GetProperty("id").GetInt32();

        var updated = McpTestClient.Json(await client.CallAsync("update_activity", new { activityId, value = "final" }));
        Assert.Equal("final", updated.GetProperty("description").GetString());
        Assert.Equal(new DateTime(2026, 3, 16, 9, 30, 0), updated.GetProperty("dateStarted").GetDateTime());

        var fetched = McpTestClient.Json(await client.CallAsync("get_activity", new { activityId }));
        Assert.Equal("final", fetched.GetProperty("primaryTagValue").GetString());
        Assert.Equal("String", fetched.GetProperty("elementName").GetString());

        var deleted = McpTestClient.Json(await client.CallAsync("delete_activity", new { activityId }));
        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("get_activity", new { activityId })));
        Assert.Equal(McpErrorMapper.NotFound, ErrorCode(await client.CallAsync("delete_activity", new { activityId })));
    }

    // --- queries ---

    [Fact]
    public async Task ListActivities_FiltersByTagAndInclusiveRange_AndClampsPageSize()
    {
        await using var client = await WriteClient();
        var (id, name) = await CreateTag(client, "Walk", InputTypeIds.Integer);
        foreach (var day in new[] { "2026-04-01", "2026-04-02", "2026-04-03", "2026-04-04" })
        {
            await client.CallAsync("log_value", new { tag = name, value = 1, dateTime = day + "T18:00" });
        }

        var page = McpTestClient.Json(await client.CallAsync("list_activities", new { tag = name, from = "2026-04-02", to = "2026-04-03", pageSize = 5000 }));

        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(PageLimits.MaxPageSize, page.GetProperty("pageSize").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, i => Assert.Equal(id, i.GetProperty("tagId").GetInt32()));
        // Newest first by default.
        Assert.Equal(new DateTime(2026, 4, 3, 18, 0, 0), items[0].GetProperty("dateStarted").GetDateTime());

        var ascending = McpTestClient.Json(await client.CallAsync("list_activities", new { tagId = id, orderBy = "asc", pageSize = 1 }));
        Assert.Equal(new DateTime(2026, 4, 1, 18, 0, 0), ascending.GetProperty("items")[0].GetProperty("dateStarted").GetDateTime());
        Assert.Equal(4, ascending.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task CheckDuplicate_AndPeriodSum_ReflectWhatWasLogged()
    {
        await using var client = await WriteClient();
        var (id, name) = await CreateTag(client, "Water", InputTypeIds.Integer, isRepeatable: false, maxValue: 8);

        var before = McpTestClient.Json(await client.CallAsync("check_duplicate", new { tagId = id, dateTime = "2026-05-01T10:00" }));
        Assert.False(before.GetProperty("exists").GetBoolean());
        Assert.Equal(new DateTime(2026, 5, 1), before.GetProperty("periodStart").GetDateTime());

        await client.CallAsync("log_value", new { tag = name, value = 3, dateTime = "2026-05-01T10:00" });

        var after = McpTestClient.Json(await client.CallAsync("check_duplicate", new { tagId = id, dateTime = "2026-05-01T23:00" }));
        Assert.True(after.GetProperty("exists").GetBoolean());

        var sum = McpTestClient.Json(await client.CallAsync("get_period_sum", new { tagId = id, dateTime = "2026-05-01T12:00" }));
        Assert.Equal(3, sum.GetProperty("currentSum").GetDouble());
        Assert.Equal(5, sum.GetProperty("remainingCapacity").GetDouble());
        Assert.True(sum.GetProperty("isNonRepeatableNumeric").GetBoolean());
        Assert.Equal(3, sum.GetProperty("existingValue").GetDouble());
    }

    [Fact]
    public async Task YearTools_ListYearsAndRows_ForOneTag()
    {
        await using var client = await WriteClient();
        var (id, name) = await CreateTag(client, "Year", InputTypeIds.Integer);
        await client.CallAsync("log_value", new { tag = name, value = 1, dateTime = "2023-06-01T10:00" });
        await client.CallAsync("log_value", new { tag = name, value = 2, dateTime = "2023-07-01T10:00" });
        await client.CallAsync("log_value", new { tag = name, value = 3, dateTime = "2024-01-01T10:00" });

        var years = McpTestClient.Json(await client.CallAsync("list_available_years", new { tagId = id }));
        Assert.Equal([2023, 2024], years.EnumerateArray().Select(y => y.GetInt32()).OrderBy(y => y));

        var rows = McpTestClient.Json(await client.CallAsync("list_activities_by_year", new { year = 2023, tagId = id }));
        Assert.Equal(2, rows.GetProperty("count").GetInt32());
        Assert.All(rows.GetProperty("items").EnumerateArray(), r => Assert.Equal(2023, r.GetProperty("dateStarted").GetDateTime().Year));
    }

    [Fact]
    public async Task ReadOnlyKey_SeesTheReadToolsOnly()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var names = (await client.ListToolsAsync()).Select(t => t.Name).ToHashSet();

        Assert.Contains("list_activities", names);
        Assert.Contains("get_period_sum", names);
        Assert.DoesNotContain("log_value", names);
        Assert.DoesNotContain("delete_activity", names);
    }
}
