using System.Security.Claims;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Domain.Enums;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Infrastructure;

/// <summary>
/// Centralizes unhandled-exception handling for API requests, returning a consistent
/// <c>ProblemDetails</c> response instead of per-action try/catch boilerplate. Non-API requests
/// (e.g. Blazor) are left to the configured error page by returning <c>false</c>.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
        {
            return false;
        }

        _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        await RecordInEventLog(httpContext, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Deliberately generic — never surface exception details to API clients.
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            }
        });
    }

    /// <summary>
    /// The Event Log is where the owner looks first, so a failure the user just hit is written
    /// there too — for the signed-in user, at Error level, with the exception as the admin-only
    /// detail. Best effort: it must never mask the original error or change the response.
    /// </summary>
    private async Task RecordInEventLog(HttpContext httpContext, Exception exception)
    {
        try
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return;
            }

            var events = httpContext.RequestServices.GetService<IEventLogService>();
            if (events == null)
            {
                return;
            }

            var message = $"API error: {httpContext.Request.Method} {httpContext.Request.Path} — {exception.GetType().Name}: {exception.Message}";
            var detail = exception.ToString();

            await events.Log(userId, EventLogLevel.Error, message, detail.Length > MaxDetailLength ? detail[..MaxDetailLength] : detail);
        }
        catch (Exception logException)
        {
            _logger.LogWarning(logException, "Could not record the API error in the event log");
        }
    }

    private const int MaxDetailLength = 8000;
}
