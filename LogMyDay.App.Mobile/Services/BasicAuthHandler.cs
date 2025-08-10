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
        System.Diagnostics.Debug.WriteLine("=== BASIC AUTH HANDLER ===");
        System.Diagnostics.Debug.WriteLine($"🔐 Adding Basic Auth header to request");
        System.Diagnostics.Debug.WriteLine($"URL: {request.RequestUri}");
        System.Diagnostics.Debug.WriteLine($"Method: {request.Method}");
        System.Diagnostics.Debug.WriteLine($"Auth Header: Basic {_credentials}");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _credentials);
        
        var response = await base.SendAsync(request, cancellationToken);
        
        System.Diagnostics.Debug.WriteLine($"📡 Response Status: {response.StatusCode} ({(int)response.StatusCode})");
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"❌ Error Response Body: {content}");
        }
        System.Diagnostics.Debug.WriteLine("=== END AUTH HANDLER ===");
        
        return response;
    }
}
