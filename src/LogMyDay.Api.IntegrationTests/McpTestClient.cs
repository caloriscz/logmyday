using System.Net.Http.Headers;
using System.Text.Json;
using LogMyDay.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// Opens a real SDK client against the test server's /mcp endpoint, so tests exercise the same
/// transport, authentication and filters an agent would.
/// </summary>
public static class McpTestClient
{
    public static Task<McpClient> ConnectAsync(CustomWebApplicationFactory factory, string token)
    {
        return ConnectAsync(factory, new AuthenticationHeaderValue("Bearer", token));
    }

    public static async Task<McpClient> ConnectAsync(CustomWebApplicationFactory factory, AuthenticationHeaderValue? authorization)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = authorization;

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(http.BaseAddress!, McpSchemaVersion.EndpointPath),
            TransportMode = HttpTransportMode.StreamableHttp,
            Name = "integration-tests"
        }, http, ownsHttpClient: true);

        return await McpClient.CreateAsync(transport);
    }

    public static Task<CallToolResult> CallAsync(this McpClient client, string tool, object? arguments = null)
    {
        var dictionary = arguments switch
        {
            null => new Dictionary<string, object?>(),
            IReadOnlyDictionary<string, object?> ready => ready,
            _ => arguments.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(arguments))
        };

        return client.CallToolAsync(tool, dictionary).AsTask();
    }

    /// <summary>The tool's single text block parsed as JSON — the shape every LogMyDay tool returns.</summary>
    public static JsonElement Json(CallToolResult result)
    {
        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;

        return JsonDocument.Parse(text).RootElement;
    }
}
