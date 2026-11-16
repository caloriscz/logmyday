using System.Net;
using System.Net.Http.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using ModelContextProtocol.Client;
using Refit;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// Tag Activity Relations checks through the real host: created over REST, they find the broken
/// days in the Rule review, over REST and over MCP.
/// </summary>
public class TagCheckIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TagCheckIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static async Task<(int Id, string Name)> CreateTag(McpClient client, string prefix, int inputTypeId)
    {
        var name = Unique(prefix);
        var result = await client.CallAsync("create_tag", new { name, inputTypeId, isRepeatable = true });
        Assert.NotEqual(true, result.IsError);

        return (McpTestClient.Json(result).GetProperty("tag").GetProperty("id").GetInt32(), name);
    }

    private HttpClient Http()
    {
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.ReadWriteKeyToken);

        return http;
    }

    [Fact]
    public async Task ACheck_FindsTheBrokenDay_OverRestAndMcp()
    {
        await using var mcp = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var (fastingId, fastingName) = await CreateTag(mcp, "Fasting", InputTypeIds.Boolean);
        var (breakfastId, breakfastName) = await CreateTag(mcp, "Breakfast", InputTypeIds.Integer);
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);
        await mcp.CallAsync("log_value", new { tag = fastingName, value = true, dateTime = $"{day:yyyy-MM-dd}T08:00" });
        await mcp.CallAsync("log_value", new { tag = breakfastName, value = 1, dateTime = $"{day:yyyy-MM-dd}T09:00" });

        var api = RestService.For<ITagCheckApi>(Http());
        var check = await api.CreateTagCheck(new TagCheckRequest
        {
            Name = Unique("Fast"),
            IfTagId = fastingId,
            IfOperator = TagRuleOperator.IsLogged,
            ThenTagId = breakfastId,
            ThenOperator = TagRuleOperator.IsNotLogged
        });
        Assert.Equal("NotOnTheSameDay", check.Kind);

        var review = await api.ReviewTagChecks(day, day);
        var item = Assert.Single(review.Items, i => i.CheckId == check.Id);
        Assert.Equal((day, "true", "1"), (item.Date, item.IfValue, item.ThenValue));

        // Over MCP: the check is listed with its kind, and the review finds the same day.
        var listed = McpTestClient.Json(await mcp.CallAsync("list_tag_checks", new { }));
        Assert.Contains(listed.EnumerateArray(), c => c.GetProperty("id").GetInt32() == check.Id && c.GetProperty("kind").GetString() == "NotOnTheSameDay");
        var one = McpTestClient.Json(await mcp.CallAsync("get_tag_check", new { checkId = check.Id }));
        Assert.Equal(breakfastId, one.GetProperty("thenTagId").GetInt32());
        var reviewed = McpTestClient.Json(await mcp.CallAsync("review_tag_checks", new { from = $"{day:yyyy-MM-dd}", to = $"{day:yyyy-MM-dd}" }));
        Assert.Contains(reviewed.GetProperty("items").EnumerateArray(), i => i.GetProperty("checkId").GetInt32() == check.Id);

        var badDate = await mcp.CallAsync("review_tag_checks", new { from = "6.5.2026" });
        Assert.True(badDate.IsError);

        await api.DeleteTagCheck(check.Id);
        Assert.DoesNotContain((await api.ReviewTagChecks(day, day)).Items, i => i.CheckId == check.Id);
    }

    [Fact]
    public async Task AWarnCheck_WarnsAfterASave_OverRestAndMcp()
    {
        await using var mcp = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var (fastingId, _) = await CreateTag(mcp, "Fasting", InputTypeIds.Boolean);
        var (breakfastId, breakfastName) = await CreateTag(mcp, "Breakfast", InputTypeIds.Integer);
        var checks = RestService.For<ITagCheckApi>(Http());
        var check = await checks.CreateTagCheck(new TagCheckRequest
        {
            Name = Unique("Fast"),
            Severity = TagCheckSeverity.Warn,
            IfTagId = fastingId,
            IfOperator = TagRuleOperator.IsLogged,
            ThenTagId = breakfastId,
            ThenOperator = TagRuleOperator.IsNotLogged
        });
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);

        // REST: a save that breaks nothing has no warnings field at all.
        var first = await Http().PostAsJsonAsync("/api/activities",
            new ActivityRequest { PrimaryTagId = fastingId, Description = "true", DateStarted = day.ToDateTime(new TimeOnly(8, 0)) });
        first.EnsureSuccessStatusCode();
        Assert.DoesNotContain("\"warnings\"", await first.Content.ReadAsStringAsync());

        // MCP: breakfast on the fasting day is saved, and the result carries the warning.
        var logged = McpTestClient.Json(await mcp.CallAsync("log_value", new { tag = breakfastName, value = 1, dateTime = $"{day:yyyy-MM-dd}T09:00" }));
        Assert.Equal("1", logged.GetProperty("storedValue").GetString());
        var warning = Assert.Single(logged.GetProperty("warnings").EnumerateArray());
        Assert.Equal(check.Name, warning.GetProperty("checkName").GetString());
        Assert.Contains("are not on the same day", warning.GetProperty("sentence").GetString());

        // REST: an update on that day warns as well.
        var activities = RestService.For<IActivityApi>(Http());
        var rowId = logged.GetProperty("activity").GetProperty("id").GetInt32();
        var updated = await activities.UpdateCalendarItem(rowId,
            new ActivityRequest { PrimaryTagId = breakfastId, Description = "2", DateStarted = day.ToDateTime(new TimeOnly(9, 0)) });
        Assert.Equal(check.Id, Assert.Single(updated.Warnings!).CheckId);

        // MCP: a record that leaves a Warn check over its fields broken.
        var type = McpTestClient.Json(await mcp.CallAsync("create_record_type", new
        {
            name = Unique("Run"),
            fields = new object[] { new { name = "Distance", inputTypeId = InputTypeIds.Decimal }, new { name = "Note", inputTypeId = InputTypeIds.String } }
        }));
        var fields = type.GetProperty("fields");
        var runCheck = await checks.CreateTagCheck(new TagCheckRequest
        {
            Name = Unique("Runs"),
            Severity = TagCheckSeverity.Warn,
            IfTagId = fields[0].GetProperty("tagId").GetInt32(),
            IfOperator = TagRuleOperator.IsLogged,
            ThenTagId = fields[1].GetProperty("tagId").GetInt32(),
            ThenOperator = TagRuleOperator.IsLogged
        });
        var record = McpTestClient.Json(await mcp.CallAsync("log_record", new
        {
            recordType = type.GetProperty("id").GetInt32().ToString(),
            dateTime = $"{day:yyyy-MM-dd}T07:00",
            values = new object[] { new { field = "Distance", value = (object)5 } }
        }));
        Assert.Equal(runCheck.Id, Assert.Single(record.GetProperty("warnings").EnumerateArray()).GetProperty("checkId").GetInt32());

        await checks.DeleteTagCheck(check.Id);
        await checks.DeleteTagCheck(runCheck.Id);
    }

    [Fact]
    public async Task RestApi_RefusesBadChecksAndRanges_WithA400()
    {
        await using var mcp = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var (tagId, _) = await CreateTag(mcp, "Solo", InputTypeIds.Integer);
        var http = Http();
        var api = RestService.For<ITagCheckApi>(http);

        var sameTag = await Assert.ThrowsAsync<ApiException>(() => api.CreateTagCheck(new TagCheckRequest
        {
            Name = "Same tag",
            IfTagId = tagId,
            ThenTagId = tagId
        }));
        Assert.Equal(HttpStatusCode.BadRequest, sameTag.StatusCode);
        Assert.Contains("two different tags", sameTag.Content);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var backwards = await Assert.ThrowsAsync<ApiException>(() => api.ReviewTagChecks(today, today.AddDays(-1)));
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/tag-checks/review?from=6.5.2026")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/tag-checks/999999")).StatusCode);
    }
}
