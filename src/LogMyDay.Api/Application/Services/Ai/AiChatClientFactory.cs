using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogMyDay.Api.Application.Options;
using OpenAI;
using OpenAI.Chat;

namespace LogMyDay.Api.Application.Services.Ai;

/// <summary>
/// Factory for creating native OpenAI ChatClient instances with runtime configuration support.
/// </summary>
public interface IAiChatClientFactory
{
    /// <summary>
    /// Gets a configured native OpenAI ChatClient. Returns null if AI is disabled or not properly configured.
    /// </summary>
    ChatClient? GetChatClient();

    /// <summary>
    /// Checks if the AI service is available and properly configured.
    /// </summary>
    bool IsAvailable();
}

/// <summary>
/// Factory implementation that creates native OpenAI chat clients with function calling support.
/// </summary>
public sealed class AiChatClientFactory : IAiChatClientFactory
{
    private readonly IOptionsMonitor<AiOptions> _optionsMonitor;
    private readonly ILogger<AiChatClientFactory> _logger;
    private ChatClient? _cachedClient;
    private string? _cachedConfigHash;

    public AiChatClientFactory(
        IOptionsMonitor<AiOptions> optionsMonitor,
        ILogger<AiChatClientFactory> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        // Subscribe to configuration changes
        _optionsMonitor.OnChange(_ =>
        {
            _logger.LogInformation("AI configuration changed, invalidating cached client");
            _cachedClient = null;
            _cachedConfigHash = null;
        });
    }

    public ChatClient? GetChatClient()
    {
        var options = _optionsMonitor.CurrentValue;

        if (!IsAvailable())
        {
            return null;
        }

        var configHash = GetConfigurationHash(options);

        if (_cachedClient is not null && _cachedConfigHash == configHash)
        {
            return _cachedClient;
        }

        try
        {
            _logger.LogInformation("Creating new AI chat client with provider: {Provider}, model: {Model}",
                options.Provider, options.Model);

            var openAiClient = new OpenAIClient(options.ApiKey);
            _cachedClient = openAiClient.GetChatClient(options.Model);

            _cachedConfigHash = configHash;

            _logger.LogInformation("AI chat client created successfully");

            return _cachedClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create AI chat client");

            return null;
        }
    }

    public bool IsAvailable()
    {
        var options = _optionsMonitor.CurrentValue;

        return options.Enabled && !string.IsNullOrWhiteSpace(options.ApiKey);
    }

    private static string GetConfigurationHash(AiOptions options)
    {
        return $"{options.Provider}_{options.Model}_{options.ApiKey}_{options.MaxTokens}_{options.Temperature}";
    }
}
