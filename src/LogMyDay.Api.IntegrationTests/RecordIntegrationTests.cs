using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LogMyDay.Domain.Constants;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Serialization;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// Records through the real host: the record type and record endpoints, a record's values in the
/// activity list, and a field refusing a write of its own over REST and MCP.
/// </summary>
public class RecordIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = JsonSerializationSettings.CreateDefault();
    private readonly CustomWebApplicationFactory _factory;

    public RecordIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.ReadWriteKeyToken);

        return client;
    }

    [Fact]
    public async Task ARecord_IsLoggedAsAWhole_AndItsFieldsRefuseWritesOfTheirOwn()
    {
        var http = Client();
        var name = $"Pressure {Guid.NewGuid():N}"[..17];
        var created = await http.PostAsJsonAsync("/api/record-types", new RecordTypeRequest
        {
            Name = name,
            Fields =
            [
                new() { Name = "Systolic", TypeId = InputTypeIds.Integer },
                new() { Name = "Pulse", TypeId = InputTypeIds.Integer }
            ]
        }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var type = (await created.Content.ReadFromJsonAsync<RecordTypeResponse>(Json))!;
        Assert.Equal([$"{name}: Systolic", $"{name}: Pulse"], type.Fields.Select(f => f.TagName));

        var when = new DateTime(2026, 5, 4, 7, 10, 0);
        var logged = await http.PostAsJsonAsync("/api/records", new RecordRequest
        {
            RecordTypeId = type.Id,
            DateStarted = when,
            Values = [new() { TagId = type.Fields[0].TagId, Value = "128" }, new() { TagId = type.Fields[1].TagId, Value = "64" }]
        }, Json);
        Assert.Equal(HttpStatusCode.Created, logged.StatusCode);
        var record = (await logged.Content.ReadFromJsonAsync<RecordResponse>(Json))!;
        Assert.Equal(["128", "64"], record.Values.Select(v => v.Value));

        // The values are ordinary activities that carry their record.
        var page = await http.GetFromJsonAsync<PagedResult<ActivityResponse>>(
            $"/api/activities?pageSize=50&tagId={type.Fields[0].TagId}", Json);
        Assert.Equal(record.Id, Assert.Single(page!.Items).RecordId);

        var single = await http.PostAsJsonAsync("/api/activities", new ActivityRequest
        {
            PrimaryTagId = type.Fields[0].TagId,
            DateStarted = when,
            Description = "120"
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, single.StatusCode);
        Assert.Contains("tag-in-record", await single.Content.ReadAsStringAsync());

        await using var mcp = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        var viaMcp = await mcp.CallAsync("log_value", new { tag = type.Fields[0].TagName, value = 120, dateTime = "2026-05-04T08:00" });
        Assert.True(viaMcp.IsError);
        Assert.Contains("record", McpTestClient.Json(viaMcp).GetProperty("hint").GetString());

        var deleted = await http.DeleteAsync($"/api/records/{record.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        page = await http.GetFromJsonAsync<PagedResult<ActivityResponse>>(
            $"/api/activities?pageSize=50&tagId={type.Fields[0].TagId}", Json);
        Assert.Empty(page!.Items);
    }
}
