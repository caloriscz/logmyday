namespace LogMyDay.App.Authentication;

/// <summary>
/// DelegatingHandler that forwards authentication cookies from the current HttpContext
/// to outgoing HTTP requests made by Refit clients. Calls made from timers or other
/// continuations without an HttpContext use the cookie remembered for the circuit.
/// </summary>
public sealed class CookieAuthenticationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CircuitRequestOrigin _origin;
    private readonly ILogger<CookieAuthenticationHandler> _logger;

    public CookieAuthenticationHandler(
        IHttpContextAccessor httpContextAccessor,
        CircuitRequestOrigin origin,
        ILogger<CookieAuthenticationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _origin = origin;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            _origin.Capture(httpContext);

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
        else if (_origin.CookieHeader is { } remembered)
        {
            request.Headers.Add("Cookie", remembered);
            _logger.LogDebug("CookieAuthenticationHandler: No HttpContext; forwarded the circuit's cookie to {RequestUri}", request.RequestUri);
        }
        else
        {
            _logger.LogWarning("CookieAuthenticationHandler: HttpContext is null and no cookie is known for this circuit; request to {RequestUri}", request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
