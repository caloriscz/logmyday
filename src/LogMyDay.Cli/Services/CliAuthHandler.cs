using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.Cli.Services;

public class CliAuthHandler : DelegatingHandler
{
    private readonly CliApiContext _ctx;

    public CliAuthHandler(CliApiContext ctx)
    {
        _ctx = ctx;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_ctx.Username is { } username && _ctx.Password is { } password)
        {
            var credentials = $"{username}:{password}";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
