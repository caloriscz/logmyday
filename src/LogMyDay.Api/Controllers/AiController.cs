using LogMyDay.Api.Application.Interfaces;
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

            if (!await _aiService.IsAvailable())
            {
                return StatusCode(503, new AiChatResponse("AI assistant is not available."));
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new AiChatResponse("Message cannot be empty."));
            }

            var response = await _aiService.Chat(request, userId);

            return Ok(response);
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
