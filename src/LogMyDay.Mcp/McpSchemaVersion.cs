namespace LogMyDay.Mcp;

/// <summary>
/// Version of the tool surface, independent of the app version. Bump the minor part for additive
/// changes (new tools, new optional parameters) and the major part for anything that would break a
/// client written against the previous surface: renames, removals, changed meanings.
/// </summary>
public static class McpSchemaVersion
{
    public const string Current = "1.0";

    public const string ServerName = "logmyday";

    public const string EndpointPath = "/mcp";
}
