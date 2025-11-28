using System.Net;
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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_ctx.Username is { } u && _ctx.Password is { } p)
        {
            var credentials = $"{u}:{p}";
            var bytes = Encoding.ASCII.GetBytes(credentials);
            var base64 = Convert.ToBase64String(bytes);
            
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64);
        }
        
        var response = await base.SendAsync(request, cancellationToken);
        
        // Handle 401 Unauthorized - credentials are invalid (e.g., password changed)
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            try
            {
                _ctx.NotifyAuthenticationExpired();
            }
            catch
            {
                // Silently handle notification errors to avoid logging sensitive context
            }
        }
        
        return response;
    }
}
