using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services.Ai;
using LogMyDay.Shared.DTOs.Ai;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/ai")]
[EnableRateLimiting("ai")]
public class AiController : BaseApiController
{
    private readonly IAiAssistantService _aiService;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IAiAssistantService aiService,
        IAuthService authService,
        ILogger<AiController> logger) : base(authService)
    {
        _aiService = aiService;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AiChatResponse>> Chat([FromBody] AiChatRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            var result = await _aiService.Chat(request, userId);

            if (result.Success)
            {
                return Ok(new AiChatResponse(result.Message, result.SuggestedActions));
            }

            // Map error codes to HTTP status codes
            var statusCode = result.ErrorCode switch
            {
                AiErrorCode.Unavailable => 503,
                AiErrorCode.ProviderError => 502,
                AiErrorCode.MaxIterationsExhausted => 502,
                AiErrorCode.InvalidRequest => 400,
                _ => 503
            };

            return StatusCode(statusCode, new AiChatResponse(result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in AI chat endpoint");

            return StatusCode(503, new AiChatResponse("AI is temporarily unavailable, please try again later."));
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<AiStatusResponse>> GetStatus()
    {
        var isAvailable = await _aiService.IsAvailable();

        return Ok(new AiStatusResponse(isAvailable));
    }
}
