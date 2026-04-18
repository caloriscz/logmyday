using LogMyDay.Shared.Interfaces;
using LogMyDay.Shared.Serialization;
using Refit;

namespace LogMyDay.Cli.Services;

public class ApiClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CliApiContext _ctx;

    private static readonly RefitSettings RefitSettings = new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(JsonSerializationSettings.CreateDefault())
    };

    public ApiClientFactory(IHttpClientFactory httpClientFactory, CliApiContext ctx)
    {
        _httpClientFactory = httpClientFactory;
        _ctx = ctx;
    }

    public IAuthApi CreateAuthApi() => Build<IAuthApi>();
    public ISecureBackupApi CreateBackupApi() => Build<ISecureBackupApi>();
    public IActivityApi CreateActivityApi() => Build<IActivityApi>();

    public HttpClient CreateHttpClient()
    {
        if (_ctx.Server is null)
        {
            throw new InvalidOperationException(
                "No active account configured. Run 'lmd login' first.");
        }

        var client = _httpClientFactory.CreateClient("lmd-api");
        client.BaseAddress = _ctx.Server;

        return client;
    }

    private T Build<T>()
    {
        if (_ctx.Server is null)
        {
            throw new InvalidOperationException(
                "No active account configured. Run 'lmd login' first.");
        }

        var client = _httpClientFactory.CreateClient("lmd-api");
        client.BaseAddress = _ctx.Server;

        return RestService.For<T>(client, RefitSettings);
    }
}
