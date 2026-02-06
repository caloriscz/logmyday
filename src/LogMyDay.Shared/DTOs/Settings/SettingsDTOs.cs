namespace LogMyDay.Shared.DTOs.Settings;

/// <summary>
/// AI configuration settings for display and updates.
/// </summary>
public sealed record AiSettingsDto(
    bool Enabled,
    string Provider,
    string Model,
    string ApiKeyMasked,
    int MaxTokens,
    float Temperature,
    int MaxConversationMessages
);

/// <summary>
/// Request to update AI settings.
/// </summary>
public sealed record UpdateAiSettingsRequest(
    bool Enabled,
    string Provider,
    string Model,
    string? ApiKey, // Null means keep existing
    int MaxTokens,
    float Temperature,
    int MaxConversationMessages
);
