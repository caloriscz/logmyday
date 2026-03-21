namespace LogMyDay.App.Authentication;

/// <summary>
/// Delegating handler that rewrites the scheme, host, and port of Refit requests to match the
/// current HTTP request context. Since Blazor Server and the API run in the same process, all
/// Refit clients call the same host — auto-detected here instead of requiring a hardcoded
/// Api:BaseAddress in configuration.
/// </summary>
internal sealed class SelfBaseAddressHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SelfBaseAddressHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null && request.RequestUri != null)
        {
            var uriBuilder = new UriBuilder(request.RequestUri)
            {
                Scheme = httpContext.Request.Scheme,
                Host = httpContext.Request.Host.Host,
                Port = httpContext.Request.Host.Port ?? -1
            };

            request.RequestUri = uriBuilder.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
