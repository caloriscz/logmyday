using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.App.Mobile.Services;

public class DynamicAuthHandler : DelegatingHandler
{
    private readonly IApiContext _ctx;
    
    public DynamicAuthHandler(IApiContext ctx)
    {
        _ctx = ctx;
        System.Diagnostics.Debug.WriteLine($"🔧 DynamicAuthHandler created. Context IsConfigured: {_ctx.IsConfigured}");
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        System.Diagnostics.Debug.WriteLine("=== DYNAMIC AUTH HANDLER DEBUG ===");
        System.Diagnostics.Debug.WriteLine($"🔐 Request URL: {request.RequestUri}");
        System.Diagnostics.Debug.WriteLine($"🔐 Request Method: {request.Method}");
        System.Diagnostics.Debug.WriteLine($"🔐 Context IsConfigured: {_ctx.IsConfigured}");
        
        if (_ctx.Username is { } u && _ctx.Password is { } p)
        {
            System.Diagnostics.Debug.WriteLine($"🔐 Username from context: '{u}'");
            System.Diagnostics.Debug.WriteLine($"🔐 Password from context: '{p}'");
            System.Diagnostics.Debug.WriteLine($"🔐 Username length: {u.Length}");
            System.Diagnostics.Debug.WriteLine($"🔐 Password length: {p.Length}");
            
            var credentials = $"{u}:{p}";
            System.Diagnostics.Debug.WriteLine($"🔐 Combined credentials: '{credentials}'");
            System.Diagnostics.Debug.WriteLine($"🔐 Combined length: {credentials.Length}");
            
            var bytes = Encoding.ASCII.GetBytes(credentials);
            var base64 = Convert.ToBase64String(bytes);
            System.Diagnostics.Debug.WriteLine($"🔐 Base64 encoded: '{base64}'");
            System.Diagnostics.Debug.WriteLine($"🔐 Base64 length: {base64.Length}");
            
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64);
            System.Diagnostics.Debug.WriteLine($"🔐 Authorization header set: 'Basic {base64}'");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"🚫 No credentials in context - Username: '{_ctx.Username}', Password: '{_ctx.Password}'");
        }
        
        System.Diagnostics.Debug.WriteLine("=== END DYNAMIC AUTH HANDLER DEBUG ===");
        return base.SendAsync(request, cancellationToken);
    }
}
