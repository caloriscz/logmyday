using Microsoft.AspNetCore.Components.Server.Circuits;

namespace LogMyDay.App.Authentication;

/// <summary>
/// Gives a circuit its origin and cookie as soon as its connection is up, and again after a
/// reconnect, so that a call without an HttpContext has them even when no other call came first.
/// </summary>
internal sealed class CircuitOriginHandler : CircuitHandler
{
    private readonly CircuitConnection _connection;
    private readonly CircuitRequestOrigin _origin;

    public CircuitOriginHandler(CircuitConnection connection, CircuitRequestOrigin origin)
    {
        _connection = connection;
        _origin = origin;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var httpContext = _connection.Current;
        if (httpContext != null)
        {
            _origin.Capture(httpContext);
        }

        return Task.CompletedTask;
    }
}
