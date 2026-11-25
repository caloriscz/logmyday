using LogMyDay.App.Authentication;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// The outgoing Refit handlers must keep working from a timer callback, where there is no
/// HttpContext: the circuit's remembered origin and cookie take over.
/// </summary>
public class CircuitRequestOriginTests
{
    private sealed class Accessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class Capture : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    /// <summary>A server that doesn't say where it listens, as IIS.</summary>
    private sealed class UnknownServer : IServer
    {
        public IFeatureCollection Features { get; } = new FeatureCollection();

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            where TContext : notnull => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private static HttpContext Context(string scheme, string host, string? cookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        if (cookie != null)
        {
            context.Request.Headers.Cookie = cookie;
        }

        return context;
    }

    private static (HttpClient Client, Capture Inner, Accessor Accessor) Pipeline(CircuitRequestOrigin origin)
    {
        var accessor = new Accessor();
        var inner = new Capture();
        var cookie = new CookieAuthenticationHandler(accessor, origin, NullLogger<CookieAuthenticationHandler>.Instance) { InnerHandler = inner };
        var self = new SelfBaseAddressHandler(accessor, origin, new OwnAddress(new UnknownServer())) { InnerHandler = cookie };

        return (new HttpClient(self) { BaseAddress = new Uri("http://localhost") }, inner, accessor);
    }

    [Fact]
    public async Task WithAContext_RewritesTheHost_ForwardsTheCookie_AndRemembersBoth()
    {
        var origin = new CircuitRequestOrigin();
        var (client, inner, accessor) = Pipeline(origin);
        accessor.HttpContext = Context("https", "lmd.example.com:9099", "lmd.auth=abc");

        await client.GetAsync("/api/tags");

        Assert.Equal("https://lmd.example.com:9099/api/tags", inner.Last!.RequestUri!.ToString());
        Assert.Equal("lmd.auth=abc", inner.Last.Headers.GetValues("Cookie").Single());
        Assert.Equal("https://lmd.example.com:9099/", origin.BaseAddress!.ToString());
        Assert.Equal("lmd.auth=abc", origin.CookieHeader);
    }

    [Fact]
    public async Task WithoutAContext_UsesTheRememberedOriginAndCookie()
    {
        var origin = new CircuitRequestOrigin();
        var (client, inner, accessor) = Pipeline(origin);
        accessor.HttpContext = Context("https", "lmd.example.com", "lmd.auth=abc");
        await client.GetAsync("/api/tags");

        // The timer tick: no HttpContext at all.
        accessor.HttpContext = null;
        await client.GetAsync("/api/reminders?date=2026-09-19");

        Assert.Equal("https://lmd.example.com/api/reminders?date=2026-09-19", inner.Last!.RequestUri!.ToString());
        Assert.Equal("lmd.auth=abc", inner.Last.Headers.GetValues("Cookie").Single());
    }

    [Fact]
    public async Task WithoutAContext_AndNothingRemembered_LeavesTheRequestAlone()
    {
        var origin = new CircuitRequestOrigin();
        var (client, inner, _) = Pipeline(origin);

        await client.GetAsync("/api/tags");

        Assert.Equal("http://localhost/api/tags", inner.Last!.RequestUri!.ToString());
        Assert.False(inner.Last.Headers.Contains("Cookie"));
    }

    [Fact]
    public async Task ARenewedCookie_ReplacesTheRememberedOne_ButAnEmptyOneDoesNot()
    {
        var origin = new CircuitRequestOrigin();
        var (client, inner, accessor) = Pipeline(origin);
        accessor.HttpContext = Context("https", "lmd.example.com", "lmd.auth=old");
        await client.GetAsync("/api/tags");
        accessor.HttpContext = Context("https", "lmd.example.com", "lmd.auth=new");
        await client.GetAsync("/api/tags");
        accessor.HttpContext = Context("https", "lmd.example.com", null);
        await client.GetAsync("/api/tags");

        accessor.HttpContext = null;
        await client.GetAsync("/api/tags");

        Assert.Equal("lmd.auth=new", inner.Last!.Headers.GetValues("Cookie").Single());
    }
}
