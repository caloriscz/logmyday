using System.Collections.Concurrent;
using LogMyDay.App.Authentication;
using LogMyDay.App.Extensions;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

    [Fact]
    public async Task ACircuitWhoseFirstCallComesFromATimer_HasItsCookieFromTheConnection()
    {
        await using var page = _services.CreateAsyncScope();
        _accessor.HttpContext = Context("lmd.auth=cookie-of-A");
        foreach (var handler in page.ServiceProvider.GetServices<CircuitHandler>())
        {
            await handler.OnConnectionUpAsync(null!, CancellationToken.None);
        }

        await Call(page, cookie: null);

        Assert.Equal(["lmd.auth=cookie-of-A"], _cookies);
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

    private sealed class Accessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
