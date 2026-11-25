using System.Collections.Concurrent;
using System.Security.Claims;
using LogMyDay.App.Authentication;
using LogMyDay.App.Extensions;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// The pages call the API from the server with the cookie of the user they belong to, and of
/// nobody else: the clients of the app's own registration, against a server that notes what it gets.
/// </summary>
public sealed class ApiClientCookieTests : IAsyncLifetime
{
    private readonly ConcurrentQueue<string> _cookies = new();
    private readonly Accessor _accessor = new();
    private WebApplication _server = null!;
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _server = builder.Build();
        _server.MapGet("/api/auth/me", (HttpContext context) =>
        {
            var cookie = context.Request.Headers.Cookie.ToString();
            _cookies.Enqueue(cookie);
            if (cookie == "lmd.auth=old-cookie-of-A")
            {
                // As the sliding renewal of a cookie past half of its life does.
                context.Response.Headers.Append("Set-Cookie", "lmd.auth=renewed-cookie-of-A; path=/; httponly; samesite=lax");
            }

            return Results.Json(new CurrentUserDto(Guid.NewGuid(), "user@example.com", null, false, "en-US", "UTC"));
        });
        await _server.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(_accessor);
        services.AddScoped<CircuitRequestOrigin>();
        services.AddRefitClients(new ConfigurationBuilder().Build());
        _services = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task ACookieRenewedForOneUser_IsNotSentWithAnothersCalls()
    {
        await using var pageOfA = _services.CreateAsyncScope();
        await using var pageOfB = _services.CreateAsyncScope();

        await Call(pageOfA, "lmd.auth=old-cookie-of-A");
        await Call(pageOfB, "lmd.auth=cookie-of-B");

        Assert.Equal(["lmd.auth=old-cookie-of-A", "lmd.auth=cookie-of-B"], _cookies);
    }

    [Fact]
    public async Task ACallFromATimer_CarriesTheCookieOfItsOwnPage()
    {
        await using var pageOfA = _services.CreateAsyncScope();
        await using var pageOfB = _services.CreateAsyncScope();

        await Call(pageOfB, "lmd.auth=cookie-of-B");
        await Call(pageOfA, "lmd.auth=cookie-of-A");

        // The timer ticks of both pages: there is no HttpContext.
        await Call(pageOfB, cookie: null);
        await Call(pageOfA, cookie: null);

        Assert.Equal(["lmd.auth=cookie-of-B", "lmd.auth=cookie-of-A", "lmd.auth=cookie-of-B", "lmd.auth=cookie-of-A"], _cookies);
    }

    // How Blazor calls the filter and the handler over either transport is not tested here; it was
    // checked in a browser with WebSockets and with long polling.
    [Fact]
    public async Task ACircuit_TakesItsCookieFromTheHubConnection_WithoutAnHttpContextAccessor()
    {
        await using var page = _services.CreateAsyncScope();
        _accessor.HttpContext = null;

        await OpenCircuit(page, "lmd.auth=cookie-of-A");
        await Call(page, cookie: null);

        Assert.Equal(["lmd.auth=cookie-of-A"], _cookies);
    }

    [Fact]
    public async Task TheHubConnectionOfOneCircuit_IsNotSeenByAnother()
    {
        await using var pageOfA = _services.CreateAsyncScope();
        await using var pageOfB = _services.CreateAsyncScope();

        await OpenCircuit(pageOfA, "lmd.auth=cookie-of-A");
        Assert.Null(_services.GetRequiredService<CircuitConnection>().Current);
        await OpenCircuit(pageOfB, "lmd.auth=cookie-of-B");
        await Call(pageOfA, cookie: null);
        await Call(pageOfB, cookie: null);

        Assert.Equal(["lmd.auth=cookie-of-A", "lmd.auth=cookie-of-B"], _cookies);
    }

    [Fact]
    public async Task ACircuitsHandler_RunningAfterTheConnectionIsGone_StillHasItsCookie()
    {
        await using var page = _services.CreateAsyncScope();
        _accessor.HttpContext = null;

        await OpenCircuit(page, "lmd.auth=cookie-of-A", connectionGone: true);
        await Call(page, cookie: null);

        Assert.Equal(["lmd.auth=cookie-of-A"], _cookies);
    }

    /// <summary>As Blazor opens a circuit: in a call of the hub, on the connection with the cookie.</summary>
    private async Task OpenCircuit(AsyncServiceScope page, string cookie, bool connectionGone = false)
    {
        var context = Context(cookie);
        var call = new HubInvocationContext(new Connection(context), page.ServiceProvider, new CircuitHub(), typeof(CircuitHub).GetMethod(nameof(CircuitHub.Start))!, []);
        await _services.GetRequiredService<CircuitConnection>().InvokeMethodAsync(call, async _ =>
        {
            if (connectionGone)
            {
                // As the server takes the request apart when the connection ends.
                ((DefaultHttpContext)context).Uninitialize();
            }

            foreach (var handler in page.ServiceProvider.GetServices<CircuitHandler>())
            {
                await handler.OnConnectionUpAsync(null!, CancellationToken.None);
            }

            return null;
        });
    }

    /// <summary>A call of a page: within a request that has the cookie, or from a timer.</summary>
    private async Task Call(AsyncServiceScope page, string? cookie)
    {
        _accessor.HttpContext = cookie == null ? null : Context(cookie);
        await page.ServiceProvider.GetRequiredService<IAuthApi>().GetCurrentUserAsync();
    }

    private HttpContext Context(string cookie)
    {
        var address = new Uri(_server.Urls.First());
        var context = new DefaultHttpContext();
        context.Request.Scheme = address.Scheme;
        context.Request.Host = new HostString(address.Host, address.Port);
        context.Request.Headers.Cookie = cookie;

        return context;
    }

    private sealed class CircuitHub : Hub
    {
        public void Start()
        {
        }
    }

    private sealed class Connection : HubCallerContext, IHttpContextFeature
    {
        public Connection(HttpContext context)
        {
            HttpContext = context;
            Features.Set<IHttpContextFeature>(this);
        }

        public HttpContext? HttpContext { get; set; }

        public override string ConnectionId => "connection";

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal? User => null;

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }

    private sealed class Accessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
