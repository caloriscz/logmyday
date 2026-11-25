namespace LogMyDay.App.Authentication;

/// <summary>
/// The HTTP client the pages call the API with. There is one per scope, a request's or a circuit's,
/// because it carries the cookie of that scope's user. IHttpClientFactory would build the handlers
/// in a scope of its own and share them between users for minutes, with the cookie they remember;
/// only the connections are shared, and they keep no cookies.
/// </summary>
internal sealed class ApiHttpClient
{
    /// <summary>The name of the shared connections at IHttpClientFactory.</summary>
    public const string Connections = "lmd-api";

    public ApiHttpClient(IHttpMessageHandlerFactory connections, IHttpContextAccessor httpContextAccessor, CircuitRequestOrigin origin,
        OwnAddress own, ILogger<CookieAuthenticationHandler> logger, Uri baseAddress)
    {
        var cookie = new CookieAuthenticationHandler(httpContextAccessor, origin, logger) { InnerHandler = connections.CreateHandler(Connections) };
        var address = new SelfBaseAddressHandler(httpContextAccessor, origin, own) { InnerHandler = cookie };

        Client = new HttpClient(address, disposeHandler: false) { BaseAddress = baseAddress };
    }

    public HttpClient Client { get; }
}
