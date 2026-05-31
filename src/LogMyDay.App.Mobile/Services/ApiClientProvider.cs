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
    ITodoApi Todo { get; }
    IEventLogApi EventLog { get; }
    IReminderApi Reminder { get; }
    ITagDayLockApi TagDayLock { get; }
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
    private ITodoApi? _todo;
    private IEventLogApi? _eventLog;
    private IReminderApi? _reminder;
    private ITagDayLockApi? _tagDayLock;

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
    public ITodoApi Todo => _todo ??= Build<ITodoApi>();
    public IEventLogApi EventLog => _eventLog ??= Build<IEventLogApi>();
    public IReminderApi Reminder => _reminder ??= Build<IReminderApi>();
    public ITagDayLockApi TagDayLock => _tagDayLock ??= Build<ITagDayLockApi>();

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
        _todo = null;
        _eventLog = null;
        _reminder = null;
        _tagDayLock = null;
    }

    public void Dispose()
    {
        _ctx.Changed -= Invalidate;
    }
}
