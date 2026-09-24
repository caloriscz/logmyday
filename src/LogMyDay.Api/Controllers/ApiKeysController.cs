using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LogMyDay.Api.Controllers;

/// <summary>
/// Self-service management of the caller's own API keys. Deliberately reachable only through a
/// browser session or Basic auth: a key must never be able to mint or revoke keys, or a stolen
/// read-write key could turn itself into a permanent one.
/// </summary>
[ApiController]
[Route("api/account/api-keys")]
[Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
[EnableRateLimiting("api")]
public class ApiKeysController : ControllerBase
{
    private readonly IApiKeyService _apiKeys;
    private readonly IAuthService _authService;

    public ApiKeysController(IApiKeyService apiKeys, IAuthService authService)
    {
        _apiKeys = apiKeys;
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<IList<ApiKeyDto>>> List(CancellationToken cancellationToken)
    {
        var userId = _authService.GetUserId(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        return Ok(await _apiKeys.List(userId.Value, cancellationToken));
    }

    /// <summary>Creates a key. The response is the only time the token is ever shown.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiKeyCreatedDto>> Create([FromBody] CreateApiKeyDto request, CancellationToken cancellationToken)
    {
        var userId = _authService.GetUserId(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<ApiKeyScope>(request.Scope, ignoreCase: true, out var scope))
        {
            return BadRequest($"Unknown scope '{request.Scope}'. Use ReadOnly or ReadWrite.");
        }

        try
        {
            var created = await _apiKeys.Create(userId.Value, request.Name, scope, request.ExpiresUtc, cancellationToken);

            return CreatedAtAction(nameof(List), created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id, CancellationToken cancellationToken)
    {
        var userId = _authService.GetUserId(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            await _apiKeys.Revoke(userId.Value, id, cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
