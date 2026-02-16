using LogMyDay.Shared.DTOs.Ai;

namespace LogMyDay.Api.Application.Services.Ai;

/// <summary>
/// Error codes for AI chat failures.
/// </summary>
public enum AiErrorCode
{
    /// <summary>
    /// AI service is unavailable or disabled.
    /// </summary>
    Unavailable,

    /// <summary>
    /// AI provider encountered an error (network, API key, model error, etc.).
    /// </summary>
    ProviderError,

    /// <summary>
    /// Invalid request from the client.
    /// </summary>
    InvalidRequest,

    /// <summary>
    /// Maximum tool-calling iterations exhausted without a final response.
    /// </summary>
    MaxIterationsExhausted
}

/// <summary>
/// Result of an AI chat operation, containing success/failure status and response data.
/// </summary>
public sealed class AiChatResult
{
    /// <summary>
    /// Gets whether the operation was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the error code if the operation failed.
    /// </summary>
    public AiErrorCode? ErrorCode { get; }

    /// <summary>
    /// Gets the user-friendly message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the suggested actions (only present on success).
    /// </summary>
    public List<AiSuggestedAction>? SuggestedActions { get; }

    private AiChatResult(bool success, AiErrorCode? errorCode, string message, List<AiSuggestedAction>? suggestedActions)
    {
        Success = success;
        ErrorCode = errorCode;
        Message = message;
        SuggestedActions = suggestedActions;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static AiChatResult Ok(string message, List<AiSuggestedAction>? suggestedActions = null)
        => new(true, null, message, suggestedActions);

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static AiChatResult Fail(AiErrorCode errorCode, string message)
        => new(false, errorCode, message, null);
}
