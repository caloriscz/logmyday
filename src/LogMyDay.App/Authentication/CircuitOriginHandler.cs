using Microsoft.AspNetCore.Components.Server.Circuits;

namespace LogMyDay.App.Authentication;

/// <summary>
/// Gives a circuit its origin and cookie as soon as its connection is up, and again after a
/// reconnect, so that a call without an HttpContext has them even when no other call came first.
/// </summary>
internal sealed class CircuitOriginHandler : CircuitHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CircuitRequestOrigin _origin;

    public CircuitOriginHandler(IHttpContextAccessor httpContextAccessor, CircuitRequestOrigin origin)
    {
        _httpContextAccessor = httpContextAccessor;
        _origin = origin;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            _origin.Capture(httpContext);
        }

        return Task.CompletedTask;
    }
}
