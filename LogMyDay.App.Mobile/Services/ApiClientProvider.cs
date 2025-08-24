using LogMyDay.Shared.Interfaces;
using Refit;
using System.Net.Http;

namespace LogMyDay.App.Mobile.Services;

public interface IApiClientProvider
{
    IActivityApi Activity { get; }
    void Invalidate();
}

public class ApiClientProvider : IApiClientProvider, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApiContext _ctx;
    private IActivityApi? _activity;

    public ApiClientProvider(IHttpClientFactory httpClientFactory, IApiContext ctx)
    {
        _httpClientFactory = httpClientFactory;
        _ctx = ctx;
        _ctx.Changed += Invalidate;
    }

    public IActivityApi Activity => _activity ??= Build<IActivityApi>();

    private T Build<T>()
    {
        if (_ctx.Server is null)
        {
            throw new InvalidOperationException("API server not configured.");
        }

        var client = _httpClientFactory.CreateClient("dynamic-api");
        client.BaseAddress = _ctx.Server; // set once per instance
        return RestService.For<T>(client);
    }

    public void Invalidate()
    {
        _activity = null;
    }

    public void Dispose()
    {
        _ctx.Changed -= Invalidate;
    }
}
