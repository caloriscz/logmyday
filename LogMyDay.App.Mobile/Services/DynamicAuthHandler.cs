using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.App.Mobile.Services;

public class DynamicAuthHandler : DelegatingHandler
{
    private readonly IApiContext _ctx;
    public DynamicAuthHandler(IApiContext ctx)
    {
        _ctx = ctx;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_ctx.Username is { } u && _ctx.Password is { } p)
        {
            var bytes = Encoding.ASCII.GetBytes($"{u}:{p}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        }
        return base.SendAsync(request, cancellationToken);
    }
}
