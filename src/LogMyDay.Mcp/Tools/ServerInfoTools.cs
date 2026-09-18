using System.ComponentModel;
using System.Reflection;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Domain.Enums;
using LogMyDay.Mcp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// The first call an agent should make: who it is acting as, what conventions the other tools
/// follow, and the reference data (input types, enums) it needs to encode values correctly.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class ServerInfoTools(McpUserContext user, IUserService users, IInputTypeService inputTypes)
{
    public const string Transport = "streamable-http";

    [McpServerTool(Name = "server_info", Title = "Server info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns LogMyDay and MCP versions, the calling key's scope, the current user (with culture and time zone), the conventions every tool follows (dates, paging, errors, confirmation sentinels), the input types with their value encoding, and the enum values tools accept. Call this first.")]
    public async Task<object> GetServerInfo(CancellationToken cancellationToken)
    {
        var account = await users.Get(user.UserId, cancellationToken);
        var types = await inputTypes.GetAllInputTypes();

        return new
        {
            appVersion = AppVersion(),
            mcpSchemaVersion = McpSchemaVersion.Current,
            sdkVersion = SdkVersion(),
            transport = Transport,
            keyScope = user.Scope,
            user = new
            {
                id = user.UserId,
                email = user.Email,
                isAdmin = user.IsAdmin,
                culture = account?.Culture,
                timeZone = account?.TimeZone
            },
            conventions = new
            {
                dates = "date = yyyy-MM-dd; date-times are naive local (no offset), exactly as stored.",
                paging = $"List tools take page (1-based) and pageSize (default {PageLimits.DefaultPageSize}, max {PageLimits.MaxPageSize}, clamped and echoed).",
                errors = "A failed call returns isError=true with JSON {code, message, ...}. Codes: not-found, invalid, tag-day-locked, conflict, confirmation-required, forbidden, rate-limited, error.",
                confirmation = "Destructive tools take a confirm argument that must equal the sentinel named in their description; a mismatch returns confirmation-required with the expected value and changes nothing.",
                scope = "Tools that change data need a read-write key; admin tools also need an admin user. A read-only key does not see them in tools/list.",
                rateLimit = $"Per key: {DestructiveBudget.PermitsPerMinute} destructive calls per minute on top of the endpoint limit."
            },
            inputTypes = types,
            enums = new
            {
                timeGranularity = Enum.GetNames<TimeGranularity>(),
                recurrenceType = Enum.GetNames<RecurrenceType>(),
                autoLogMode = Enum.GetNames<AutoLogMode>(),
                eventLogLevel = Enum.GetNames<EventLogLevel>(),
                apiKeyScope = Enum.GetNames<ApiKeyScope>()
            }
        };
    }

    /// <summary>The hosting application's version; the MCP library never knows the App type.</summary>
    public static string AppVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ServerInfoTools).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return informational?.Split('+')[0] ?? assembly.GetName().Version?.ToString() ?? "unknown";
    }

    public static string SdkVersion()
    {
        var assembly = typeof(McpServer).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return informational?.Split('+')[0] ?? assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
