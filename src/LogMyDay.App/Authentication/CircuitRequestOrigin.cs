namespace LogMyDay.App.Authentication;

/// <summary>
/// What the outgoing Refit handlers need from the current HTTP request — where this app is
/// reachable and the user's session cookie — remembered per circuit. Blazor Server components run
/// timers and other continuations on threads where <see cref="IHttpContextAccessor.HttpContext"/>
/// is null; without this fallback such calls went to the placeholder host with no cookie. The
/// values are refreshed whenever a request with a context passes through, so a renewed cookie is
/// picked up. Nothing here leaves the server: the circuit's DI scope holds it, as the HttpContext
/// itself did.
/// </summary>
public sealed class CircuitRequestOrigin
{
    private readonly object _gate = new();

    public Uri? BaseAddress { get; private set; }

    public string? CookieHeader { get; private set; }

    public void Capture(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var baseAddress = new UriBuilder
        {
            Scheme = request.Scheme,
            Host = request.Host.Host,
            Port = request.Host.Port ?? -1
        }.Uri;
        var cookie = request.Headers.Cookie.FirstOrDefault();

        lock (_gate)
        {
            BaseAddress = baseAddress;
            if (!string.IsNullOrEmpty(cookie))
            {
                CookieHeader = cookie;
            }
        }
    }
}
