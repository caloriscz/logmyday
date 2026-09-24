using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LogMyDay.Shared.Serialization;

namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// The one serializer configuration for tool arguments and results: the same options the REST API
/// and every client already use, so an agent sees the wire format it would see over HTTP —
/// camelCase, enums as their names.
/// </summary>
public static class McpJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = JsonSerializationSettings.CreateDefault();
        // The SDK freezes the options when it builds tool schemas, which needs an explicit resolver.
        options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();

        return options;
    }
}
