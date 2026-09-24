using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// The one filter around every tool call. In order: refuse a write tool on a read-only key even if
/// a tool forgot its policy attribute; meter destructive tools; run the tool with every exception
/// mapped to an error result; and audit anything that was not read-only through the event log,
/// with the arguments attached and secrets redacted.
/// </summary>
public static class McpToolFilters
{
    public const string AuditPrefix = "MCP ";
    public const string RateLimited = "rate-limited";

    private const int MaxAuditDetailChars = 2048;

    private static readonly HashSet<string> RedactedArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "newPassword", "currentPassword", "token", "apiKey", "secret", "backup"
    };

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> AuditAndMapErrors(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (context, cancellationToken) =>
        {
            var services = context.Services
                ?? throw new InvalidOperationException("MCP request has no service scope; ScopeRequests must be enabled.");

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("LogMyDay.Mcp");
            var user = services.GetRequiredService<McpUserContext>();
            var toolName = context.Params?.Name ?? "(unknown)";

            var annotations = (context.MatchedPrimitive as McpServerTool)?.ProtocolTool.Annotations;
            var isReadOnly = annotations?.ReadOnlyHint == true;
            // The SDK default for DestructiveHint is true, which is why every tool sets it explicitly.
            var isDestructive = !isReadOnly && annotations?.DestructiveHint != false;

            if (!isReadOnly && !user.CanWrite)
            {
                return McpErrorMapper.Error(McpErrorMapper.Forbidden, $"{toolName} changes data and this key is read-only.");
            }

            if (isDestructive && !services.GetRequiredService<DestructiveBudget>().TryAcquire(user.ApiKeyId))
            {
                return McpErrorMapper.Error(RateLimited,
                    $"Too many destructive calls in the last minute (limit {DestructiveBudget.PermitsPerMinute}). Wait before calling {toolName} again.");
            }

            CallToolResult result;
            try
            {
                result = await next(context, cancellationToken);
            }
            catch (Exception ex)
            {
                result = McpErrorMapper.ToResult(ex, logger);
            }

            if (!isReadOnly)
            {
                await AuditAsync(services, user, toolName, context.Params?.Arguments, result, logger);
            }

            return result;
        };
    }

    private static async Task AuditAsync(
        IServiceProvider services,
        McpUserContext user,
        string toolName,
        IDictionary<string, JsonElement>? arguments,
        CallToolResult result,
        ILogger logger)
    {
        var failed = result.IsError == true;
        var message = $"{AuditPrefix}{toolName} via {user.KeyPrefix}: {(failed ? "error" : "ok")}";

        // Auditing must never turn a successful tool call into a failed one.
        try
        {
            await services.GetRequiredService<IEventLogService>()
                .Log(user.UserId, failed ? EventLogLevel.Error : EventLogLevel.Info, message, DescribeArguments(arguments));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit log write failed for {Tool}", toolName);
        }
    }

    /// <summary>The call's arguments as JSON for the audit row, secrets blanked, size capped.</summary>
    public static string? DescribeArguments(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments == null || arguments.Count == 0)
        {
            return null;
        }

        var safe = new Dictionary<string, object?>(arguments.Count);
        foreach (var (key, value) in arguments)
        {
            safe[key] = RedactedArguments.Contains(key) ? "[redacted]" : value;
        }

        var json = JsonSerializer.Serialize(safe, McpJson.Options);

        return json.Length <= MaxAuditDetailChars ? json : json[..MaxAuditDetailChars] + "…";
    }
}
