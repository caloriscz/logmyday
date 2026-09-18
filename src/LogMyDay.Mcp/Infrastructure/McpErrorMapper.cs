using System.Text.Json;
using LogMyDay.Api.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// Turns an exception from an application service into the error result an agent can act on. This
/// is the controllers' exception-to-status mapping done once for every tool, so tools carry no
/// try/catch of their own. The result is <c>isError</c> with a small JSON body: a stable
/// <c>code</c>, a message where the service's message is already user-facing, and any detail
/// (e.g. which tag and day are locked) that lets the agent do the right next thing.
/// </summary>
public static class McpErrorMapper
{
    public const string NotFound = "not-found";
    public const string Invalid = "invalid";
    public const string TagDayLocked = "tag-day-locked";
    public const string Conflict = "conflict";
    public const string ConfirmationRequired = "confirmation-required";
    public const string Forbidden = "forbidden";
    public const string Unexpected = "error";

    public static CallToolResult ToResult(Exception exception, ILogger logger)
    {
        switch (exception)
        {
            case McpException:
                throw exception;

            case KeyNotFoundException ex:
                return Error(NotFound, ex.Message);

            case ConfirmationRequiredException ex:
                return Error(ConfirmationRequired, ex.Message, new { expected = ex.Expected, impact = ex.Impact });

            case TagDayLockedException ex:
                return Error(TagDayLocked,
                    $"Tag {ex.TagId} is locked for {ex.Date:yyyy-MM-dd}; nothing was logged.",
                    new { tagId = ex.TagId, date = ex.Date.ToString("yyyy-MM-dd"), hint = "Unlock the day with set_tag_day_lock, or log to another day." });

            case UnauthorizedAccessException ex:
                return Error(Forbidden, ex.Message);

            // The services throw these with messages written for the UI, so they are safe to relay.
            case ArgumentException ex:
                return Error(Invalid, ex.Message);

            // "X is in use by one or more tags" is the services' way of refusing a delete that a
            // Restrict FK would otherwise turn into a database error.
            case InvalidOperationException ex when ex.Message.Contains("in use", StringComparison.OrdinalIgnoreCase)
                                                    || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase):
                return Error(Conflict, ex.Message);

            case InvalidOperationException ex:
                return Error(Invalid, ex.Message);

            case DbUpdateException ex when ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true:
                return Error(Conflict, "A record with the same key already exists.");

            case DbUpdateException ex when ex.InnerException?.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) == true:
                return Error(Conflict, "The record is still referenced by another record.");

            default:
                var correlationId = Guid.NewGuid().ToString("N")[..12];
                logger.LogError(exception, "Unhandled exception in MCP tool (correlation {CorrelationId})", correlationId);

                return Error(Unexpected, "Unexpected error; see the server log.", new { correlationId });
        }
    }

    public static CallToolResult Error(string code, string message, object? detail = null)
    {
        var payload = new Dictionary<string, object?> { ["code"] = code, ["message"] = message };

        if (detail != null)
        {
            foreach (var property in detail.GetType().GetProperties())
            {
                payload[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = property.GetValue(detail);
            }
        }

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(payload, McpJson.Options) }]
        };
    }
}

/// <summary>
/// Thrown by a guarded tool when its <c>confirm</c> argument does not match the sentinel the tool
/// description spells out. Nothing has been touched when this is thrown.
/// </summary>
public sealed class ConfirmationRequiredException : Exception
{
    public ConfirmationRequiredException(string expected, string? impact = null)
        : base($"This action is destructive. Call again with confirm set to exactly \"{expected}\".")
    {
        Expected = expected;
        Impact = impact;
    }

    public string Expected { get; }

    /// <summary>What the call would do, in words the agent should relay before confirming.</summary>
    public string? Impact { get; }
}
