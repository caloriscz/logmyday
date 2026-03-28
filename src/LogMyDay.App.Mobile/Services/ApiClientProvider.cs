using LogMyDay.Shared.Interfaces;
using LogMyDay.Shared.Serialization;
using Refit;
using System.Net.Http;

namespace LogMyDay.App.Mobile.Services;

public interface IApiClientProvider
{
    IActivityApi Activity { get; }
    IAuthApi Auth { get; }
    IUsersApi Users { get; }
    IAccountApi Account { get; }
    IScanMappingApi ScanMapping { get; }
    ITagGroupApi TagGroup { get; }
    IAiApi Ai { get; }
    void Invalidate();
}

public class ApiClientProvider : IApiClientProvider, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApiContext _ctx;
    private IActivityApi? _activity;
    private IAuthApi? _auth;
    private IUsersApi? _users;
    private IAccountApi? _account;
    private IScanMappingApi? _scanMapping;
    private ITagGroupApi? _tagGroup;
    private IAiApi? _ai;

    public ApiClientProvider(IHttpClientFactory httpClientFactory, IApiContext ctx)
    {
        _httpClientFactory = httpClientFactory;
        _ctx = ctx;
        _ctx.Changed += Invalidate;
    }

    public IActivityApi Activity => _activity ??= Build<IActivityApi>();
    public IAuthApi Auth => _auth ??= Build<IAuthApi>();
    public IUsersApi Users => _users ??= Build<IUsersApi>();
    public IAccountApi Account => _account ??= Build<IAccountApi>();
    public IScanMappingApi ScanMapping => _scanMapping ??= Build<IScanMappingApi>();
    public ITagGroupApi TagGroup => _tagGroup ??= Build<ITagGroupApi>();
    public IAiApi Ai => _ai ??= Build<IAiApi>();

    private static readonly RefitSettings SharedRefitSettings = new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(JsonSerializationSettings.CreateDefault())
    };

    private T Build<T>()
    {
        if (_ctx.Server is null)
        {
            throw new InvalidOperationException("API server not configured.");
        }

        var client = _httpClientFactory.CreateClient("dynamic-api");
        client.BaseAddress = _ctx.Server; // set once per instance
        return RestService.For<T>(client, SharedRefitSettings);
    }

    public void Invalidate()
    {
        _activity = null;
        _auth = null;
        _users = null;
        _account = null;
        _scanMapping = null;
        _tagGroup = null;
        _ai = null;
    }

    public void Dispose()
    {
        _ctx.Changed -= Invalidate;
    }
}
