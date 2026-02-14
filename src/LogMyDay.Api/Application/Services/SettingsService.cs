using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Options;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace LogMyDay.Api.Application.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly LogMyDayDbContext _context;
    private readonly IOptionsMonitor<AiOptions> _defaultAiOptions;
    private readonly ILogger<SettingsService> _logger;
    private readonly ISettingProtector _protector;

    public SettingsService(
        LogMyDayDbContext context,
        IOptionsMonitor<AiOptions> defaultAiOptions,
        ILogger<SettingsService> logger,
        ISettingProtector protector)
    {
        _context = context;
        _defaultAiOptions = defaultAiOptions;
        _logger = logger;
        _protector = protector;
    }

    public async Task<AiOptions> GetAiOptionsAsync(CancellationToken cancellationToken = default)
    {
        var defaultOptions = _defaultAiOptions.CurrentValue;

        try
        {
            var enabled = await GetSettingAsync("AI:Enabled", cancellationToken);
            var provider = await GetSettingAsync("AI:Provider", cancellationToken);
            var model = await GetSettingAsync("AI:Model", cancellationToken);
            var apiKey = await GetSettingAsync("AI:ApiKey", cancellationToken);
            var maxTokens = await GetSettingAsync("AI:MaxTokens", cancellationToken);
            var temperature = await GetSettingAsync("AI:Temperature", cancellationToken);
            var maxMessages = await GetSettingAsync("AI:MaxConversationMessages", cancellationToken);

            // Decrypt API key if it exists
            string? decryptedApiKey = null;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    decryptedApiKey = _protector.Unprotect(apiKey);
                }
                catch (CryptographicException)
                {
                    // API key is not encrypted (legacy plain-text value)
                    // Use it as-is; next save will encrypt it
                    _logger.LogWarning("API key is stored in plain text, will be encrypted on next update");
                    decryptedApiKey = apiKey;
                }
            }

            return new AiOptions
            {
                Enabled = bool.TryParse(enabled, out var e) ? e : defaultOptions.Enabled,
                Provider = provider ?? defaultOptions.Provider,
                Model = model ?? defaultOptions.Model,
                ApiKey = decryptedApiKey ?? defaultOptions.ApiKey,
                MaxTokens = int.TryParse(maxTokens, out var mt) ? mt : defaultOptions.MaxTokens,
                Temperature = float.TryParse(temperature, out var t) ? t : defaultOptions.Temperature,
                MaxConversationMessages = int.TryParse(maxMessages, out var mm) ? mm : defaultOptions.MaxConversationMessages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading AI options from database, using defaults");

            return defaultOptions;
        }
    }

    public async Task UpdateAiOptionsAsync(AiOptions options, CancellationToken cancellationToken = default)
    {
        await SetSettingAsync("AI:Enabled", options.Enabled.ToString(), "Enable or disable AI assistant", cancellationToken);
        await SetSettingAsync("AI:Provider", options.Provider, "AI provider (e.g., openai)", cancellationToken);
        await SetSettingAsync("AI:Model", options.Model, "AI model name", cancellationToken);
        
        // Encrypt API key before storing
        var encryptedApiKey = string.IsNullOrWhiteSpace(options.ApiKey) 
            ? options.ApiKey 
            : _protector.Protect(options.ApiKey);
        await SetSettingAsync("AI:ApiKey", encryptedApiKey, "AI API key (encrypted)", cancellationToken);
        
        await SetSettingAsync("AI:MaxTokens", options.MaxTokens.ToString(), "Maximum tokens per response", cancellationToken);
        await SetSettingAsync("AI:Temperature", options.Temperature.ToString("F2"), "AI temperature (creativity)", cancellationToken);
        await SetSettingAsync("AI:MaxConversationMessages", options.MaxConversationMessages.ToString(), "Max conversation history", cancellationToken);

        _logger.LogInformation("AI settings updated in database");
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _context.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        return setting?.Value;
    }

    public async Task SetSettingAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (existing is not null)
        {
            existing.Value = value;
            existing.UpdatedUtc = DateTime.UtcNow;

            if (description is not null)
            {
                existing.Description = description;
            }
        }
        else
        {
            _context.Settings.Add(new Setting
            {
                Key = key,
                Value = value,
                Description = description
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
