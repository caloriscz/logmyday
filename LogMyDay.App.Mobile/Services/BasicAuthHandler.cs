using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.App.Mobile.Services;

public class BasicAuthHandler : DelegatingHandler
{
    private readonly string _credentials;

    public BasicAuthHandler(string username, string password)
    {
        _credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _credentials);
        
        return await base.SendAsync(request, cancellationToken);
    }
}
