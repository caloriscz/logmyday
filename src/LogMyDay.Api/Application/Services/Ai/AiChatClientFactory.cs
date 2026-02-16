using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using LogMyDay.Api.Application.Options;
using LogMyDay.Api.Application.Interfaces;
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
    Task<ChatClient?> GetChatClient();

    /// <summary>
    /// Checks if the AI service is available and properly configured.
    /// </summary>
    Task<bool> IsAvailable();
}

/// <summary>
/// Factory implementation that creates native OpenAI chat clients with function calling support.
/// </summary>
public sealed class AiChatClientFactory : IAiChatClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiChatClientFactory> _logger;
    private ChatClient? _cachedClient;
    private string? _cachedConfigHash;

    public AiChatClientFactory(
        IServiceProvider serviceProvider,
        ILogger<AiChatClientFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<ChatClient?> GetChatClient()
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var options = await settingsService.GetAiOptionsAsync();

        if (!await IsAvailable())
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

    public async Task<bool> IsAvailable()
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var options = await settingsService.GetAiOptionsAsync();

        return options.Enabled && !string.IsNullOrWhiteSpace(options.ApiKey);
    }

    private static string GetConfigurationHash(AiOptions options)
    {
        return $"{options.Provider}_{options.Model}_{options.ApiKey}_{options.MaxTokens}_{options.Temperature}";
    }
}
