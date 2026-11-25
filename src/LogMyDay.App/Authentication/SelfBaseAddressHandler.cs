namespace LogMyDay.App.Authentication;

/// <summary>
/// Delegating handler that rewrites the scheme, host, and port of Refit requests to match the
/// current HTTP request context. Since Blazor Server and the API run in the same process, all
/// Refit clients call the same host — auto-detected here instead of requiring a hardcoded
/// Api:BaseAddress in configuration. When the call is made from a timer or another continuation
/// with no HttpContext, the origin remembered for the circuit is used instead.
/// </summary>
internal sealed class SelfBaseAddressHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CircuitRequestOrigin _origin;

    public SelfBaseAddressHandler(IHttpContextAccessor httpContextAccessor, CircuitRequestOrigin origin)
    {
        _httpContextAccessor = httpContextAccessor;
        _origin = origin;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            _origin.Capture(httpContext);
        }

        var baseAddress = _origin.BaseAddress;
        if (baseAddress != null && request.RequestUri != null)
        {
            var uriBuilder = new UriBuilder(request.RequestUri)
            {
                Scheme = baseAddress.Scheme,
                Host = baseAddress.Host,
                Port = baseAddress.IsDefaultPort ? -1 : baseAddress.Port
            };

            request.RequestUri = uriBuilder.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
