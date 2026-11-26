using Microsoft.AspNetCore.SignalR;

namespace LogMyDay.App.Authentication;

/// <summary>
/// The origin of the connection whose hub call is running. <see cref="IHttpContextAccessor"/> has
/// its request with WebSockets only: with long polling, the fallback where WebSockets are blocked,
/// each poll is a request of its own and the accessor is empty.
/// </summary>
internal sealed class CircuitConnection : IHubFilter
{
    private readonly AsyncLocal<RequestOrigin?> _current = new();

    public RequestOrigin? Current => _current.Value;

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        // Seen by what this call starts, such as the circuit and its handlers, and by nothing else.
        // They may run after the connection is gone, so they get a copy, not the HttpContext.
        _current.Value = invocationContext.Context.GetHttpContext() is { } httpContext ? RequestOrigin.Of(httpContext) : null;

        return await next(invocationContext);
    }
}
