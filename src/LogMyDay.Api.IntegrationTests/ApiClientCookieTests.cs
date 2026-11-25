using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using LogMyDay.App.Authentication;
using LogMyDay.App.Extensions;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
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
        services.AddSingleton(_server.Services.GetRequiredService<IServer>());
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

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    [InlineData("attacker.invalid")]
    public async Task TheHostHeader_DoesNotChooseWhereTheCallGoes(string host)
    {
        var elsewhere = new TcpListener(IPAddress.Loopback, 0);
        elsewhere.Start();
        var port = ((IPEndPoint)elsewhere.LocalEndpoint).Port;
        await using var page = _services.CreateAsyncScope();
        _accessor.HttpContext = Context("lmd.auth=cookie-of-A");
        _accessor.HttpContext.Request.Host = new HostString(host, port);

        await page.ServiceProvider.GetRequiredService<IAuthApi>().GetCurrentUserAsync();

        Assert.Equal(["lmd.auth=cookie-of-A"], _cookies);
        Assert.False(elsewhere.Pending(), "The server called the port that the Host header named.");
        elsewhere.Stop();
    }

    /// <summary>As Blazor opens a circuit: in a call of the hub, on the connection with the cookie.</summary>
    private async Task OpenCircuit(AsyncServiceScope page, string cookie)
    {
        var call = new HubInvocationContext(new Connection(Context(cookie)), page.ServiceProvider, new CircuitHub(), typeof(CircuitHub).GetMethod(nameof(CircuitHub.Start))!, []);
        await _services.GetRequiredService<CircuitConnection>().InvokeMethodAsync(call, async _ =>
        {
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
