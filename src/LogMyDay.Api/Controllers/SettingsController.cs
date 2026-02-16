using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Policy = "AdminOnly")]
public class SettingsController : BaseApiController
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsService settingsService,
        IAuthService authService,
        ILogger<SettingsController> logger) : base(authService)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    [HttpGet("ai")]
    public async Task<ActionResult<AiSettingsDto>> GetAiSettings(CancellationToken cancellationToken)
    {
        try
        {
            var options = await _settingsService.GetAiOptionsAsync(cancellationToken);

            var maskedApiKey = MaskApiKey(options.ApiKey);

            return Ok(new AiSettingsDto(
                options.Enabled,
                options.Provider,
                options.Model,
                maskedApiKey,
                options.MaxTokens,
                options.Temperature,
                options.MaxConversationMessages
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching AI settings");

            return StatusCode(500, "Failed to fetch AI settings");
        }
    }

    [HttpPut("ai")]
    public async Task<ActionResult> UpdateAiSettings([FromBody] UpdateAiSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Get current options to preserve API key if not provided
            var currentOptions = await _settingsService.GetAiOptionsAsync(cancellationToken);

            var apiKey = string.IsNullOrWhiteSpace(request.ApiKey)
                ? currentOptions.ApiKey
                : request.ApiKey!;

            var updatedOptions = new Application.Options.AiOptions
            {
                Enabled = request.Enabled,
                Provider = request.Provider,
                Model = request.Model,
                ApiKey = apiKey,
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                MaxConversationMessages = request.MaxConversationMessages
            };

            await _settingsService.UpdateAiOptionsAsync(updatedOptions, cancellationToken);

            var userId = GetCurrentUserId();
            _logger.LogInformation("AI settings updated by user {UserId}", userId);

            return Ok(new { Message = "AI settings updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating AI settings");

            return StatusCode(500, "Failed to update AI settings");
        }
    }

    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        if (apiKey.Length <= 4)
        {
            return "****";
        }

        var lastFour = apiKey[^4..];

        return $"**************{lastFour}";
    }
}
