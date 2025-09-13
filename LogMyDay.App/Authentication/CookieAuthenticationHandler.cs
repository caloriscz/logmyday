using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace LogMyDay.App.Authentication;

/// <summary>
/// DelegatingHandler that forwards authentication cookies from the current HttpContext 
/// to outgoing HTTP requests made by Refit clients.
/// </summary>
public sealed class CookieAuthenticationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CookieAuthenticationHandler> _logger;

    public CookieAuthenticationHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<CookieAuthenticationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext != null)
        {
            // Forward authentication cookies from the current request to the API call
            var cookieHeader = httpContext.Request.Headers["Cookie"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.Add("Cookie", cookieHeader);
                _logger.LogDebug("CookieAuthenticationHandler: Forwarded cookies to {RequestUri}", request.RequestUri);
            }
            else
            {
                _logger.LogDebug("CookieAuthenticationHandler: No cookies found in current HttpContext for {RequestUri}", request.RequestUri);
            }
            
            // Log current authentication state for debugging
            _logger.LogDebug("CookieAuthenticationHandler: User authenticated: {IsAuthenticated}, User: {UserName}", 
                httpContext.User?.Identity?.IsAuthenticated, 
                httpContext.User?.Identity?.Name ?? "null");
        }
        else
        {
            _logger.LogWarning("CookieAuthenticationHandler: HttpContext is null for request to {RequestUri}", request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}