using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.App.Mobile.Services;

public class AuthenticationHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            // Get credentials from preferences
            var username = Preferences.Get("Username", "");
            var password = Preferences.Get("Password", "");

            System.Diagnostics.Debug.WriteLine($"[AuthHandler] Request URL: {request.RequestUri}");
            System.Diagnostics.Debug.WriteLine($"[AuthHandler] Username from prefs: '{username}'");
            System.Diagnostics.Debug.WriteLine($"[AuthHandler] Password length: {password.Length}");

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
                var base64String = Convert.ToBase64String(byteArray);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64String);
                
                System.Diagnostics.Debug.WriteLine($"[AuthHandler] ✅ Injected Basic Auth for user '{username}'");
                System.Diagnostics.Debug.WriteLine($"[AuthHandler] Auth header: Basic {base64String}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AuthHandler] ❌ No credentials present - request will fail");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthHandler] ❌ Error setting auth header: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[AuthHandler] Stack trace: {ex.StackTrace}");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
