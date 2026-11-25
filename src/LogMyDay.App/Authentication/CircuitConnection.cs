using Microsoft.AspNetCore.SignalR;

namespace LogMyDay.App.Authentication;

/// <summary>
/// The HTTP request of the connection whose hub call is running. <see cref="IHttpContextAccessor"/>
/// has it with WebSockets only: with long polling, the fallback where WebSockets are blocked, each
/// poll is a request of its own and the accessor is empty.
/// </summary>
internal sealed class CircuitConnection : IHubFilter
{
    private readonly AsyncLocal<HttpContext?> _current = new();

    public HttpContext? Current => _current.Value;

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        // Seen by what this call starts, such as the circuit and its handlers, and by nothing else.
        _current.Value = invocationContext.Context.GetHttpContext();

        return await next(invocationContext);
    }
}
