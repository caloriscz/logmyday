using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Mcp;
using LogMyDay.Mcp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// The /mcp endpoint end to end: who gets in, what a key sees, and that server_info answers.
/// Later tool tasks add their own cases next to these.
/// </summary>
public class McpEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public McpEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpResponseMessage> PostInitializeAsync(AuthenticationHeaderValue? authorization)
    {
        const string body = """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"raw","version":"1"}}}
            """;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = authorization;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        return await client.PostAsync(McpSchemaVersion.EndpointPath, new StringContent(body, Encoding.UTF8, "application/json"));
    }

    // --- Who gets in ---

    [Fact]
    public async Task NoCredentials_Is401_WithBearerChallenge()
    {
        var response = await PostInitializeAsync(null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task BasicAuth_Is403_TheEndpointRequiresAnApiKeyPrincipal()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            CustomWebApplicationFactory.TestUserEmail + ":" + CustomWebApplicationFactory.TestUserPassword));

        var response = await PostInitializeAsync(new AuthenticationHeaderValue("Basic", credentials));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownKey_Is401()
    {
        var response = await PostInitializeAsync(new AuthenticationHeaderValue("Bearer", "lmd_" + new string('x', 43)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReadWriteKey_Connects_AndSeesTheServerIdentity()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

        Assert.Equal(McpSchemaVersion.ServerName, client.ServerInfo.Name);
        Assert.False(string.IsNullOrWhiteSpace(client.ServerInstructions));
    }

    // --- What a key sees ---

    [Fact]
    public async Task ToolsList_EveryToolHasExplicitAnnotations()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

        var tools = await client.ListToolsAsync();

        Assert.NotEmpty(tools);
        Assert.All(tools, tool =>
        {
            var annotations = tool.ProtocolTool.Annotations;
            Assert.NotNull(annotations);
            Assert.NotNull(annotations.ReadOnlyHint);
            Assert.NotNull(annotations.DestructiveHint);
            Assert.NotNull(annotations.IdempotentHint);
            Assert.NotNull(annotations.OpenWorldHint);
        });
    }

    [Fact]
    public async Task ReadOnlyKey_SeesOnlyReadOnlyTools()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var tools = await client.ListToolsAsync();

        Assert.Contains(tools, t => t.Name == "server_info");
        Assert.All(tools, t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint, t.Name + " is visible to a read-only key"));
    }

    [Fact]
    public async Task UnknownTool_IsAnErrorResult_NamingTheTool()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var result = await client.CallAsync("no_such_tool");

        Assert.True(result.IsError);
        Assert.Contains("no_such_tool", Assert.Single(result.Content.OfType<TextContentBlock>()).Text);
    }

    // --- server_info ---

    [Fact]
    public async Task ServerInfo_RoundTrips_WithVersionsScopeUserAndReferenceData()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);

        var result = await client.CallAsync("server_info");

        Assert.NotEqual(true, result.IsError);
        var info = McpTestClient.Json(result);

        Assert.Equal(McpSchemaVersion.Current, info.GetProperty("mcpSchemaVersion").GetString());
        Assert.Equal("2.2.0", info.GetProperty("sdkVersion").GetString());
        Assert.Equal("streamable-http", info.GetProperty("transport").GetString());
        Assert.Equal("ReadWrite", info.GetProperty("keyScope").GetString());
        Assert.Equal(CustomWebApplicationFactory.TestUserEmail, info.GetProperty("user").GetProperty("email").GetString());
        Assert.False(string.IsNullOrEmpty(info.GetProperty("user").GetProperty("timeZone").GetString()));
        Assert.True(info.GetProperty("inputTypes").GetArrayLength() > 0);
        Assert.Contains("Daily", info.GetProperty("enums").GetProperty("timeGranularity").EnumerateArray().Select(e => e.GetString()));

        var paging = info.GetProperty("conventions").GetProperty("paging").GetString()!;
        Assert.Equal(PageLimits.MaxPageSize.ToString(), Regex.Match(paging, @"max (\d+)").Groups[1].Value);
    }

    [Fact]
    public async Task ServerInfo_ReportsTheKeyScope_ForAReadOnlyKey()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadOnlyKeyToken);

        var info = McpTestClient.Json(await client.CallAsync("server_info"));

        Assert.Equal("ReadOnly", info.GetProperty("keyScope").GetString());
    }

    [Fact]
    public async Task ReadOnlyTool_WritesNoAuditRow()
    {
        await using var client = await McpTestClient.ConnectAsync(_factory, CustomWebApplicationFactory.ReadWriteKeyToken);
        await client.CallAsync("server_info");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();

        Assert.False(await db.EventLogs.AnyAsync(e => e.Message.StartsWith(McpToolFilters.AuditPrefix + "server_info")));
    }
}
