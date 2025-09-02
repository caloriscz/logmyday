using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LogMyDay.Shared.Interfaces;

namespace LogMyDay.App.Mobile.Services;

/// <summary>
/// Enhanced authentication handler that automatically retries authentication
/// when receiving 401 Unauthorized responses, if stored credentials are available.
/// </summary>
public class AutoRetryAuthHandler : DelegatingHandler
{
    private readonly IApiContext _apiContext;
    private readonly AuthenticationService _authService;

    public AutoRetryAuthHandler(IApiContext apiContext, AuthenticationService authService)
    {
        _apiContext = apiContext;
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Add authentication header if we have credentials
        AddAuthenticationHeader(request);

        // Send the initial request
        var response = await base.SendAsync(request, cancellationToken);

        // If we get 401 and have stored credentials, try automatic re-authentication
        if (response.StatusCode == HttpStatusCode.Unauthorized && CanAttemptAutoReauth())
        {
            System.Diagnostics.Debug.WriteLine(
                "🔄 [AutoRetryAuthHandler] Received 401 - attempting automatic re-authentication");

            var reAuthSuccess = await AttemptAutoReAuthentication();
            if (reAuthSuccess)
            {
                System.Diagnostics.Debug.WriteLine(
                    "✅ [AutoRetryAuthHandler] Auto re-authentication successful - retrying original request");

                // Clone the original request (we can't reuse the same request object)
                var retryRequest = await CloneRequest(request);

                // Add the refreshed authentication header
                AddAuthenticationHeader(retryRequest);

                // Retry the original request
                var retryResponse = await base.SendAsync(retryRequest, cancellationToken);

                if (retryResponse.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "🎉 [AutoRetryAuthHandler] Retry successful after auto re-authentication");

                    return retryResponse;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"❌ [AutoRetryAuthHandler] Retry failed with status: {retryResponse.StatusCode}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    "❌ [AutoRetryAuthHandler] Auto re-authentication failed - clearing auth and redirecting to login");
                ClearAuthenticationAndRedirect();
            }
        }

        return response;
    }

    private void AddAuthenticationHeader(HttpRequestMessage request)
    {
        if (_apiContext.Username is { } username && _apiContext.Password is { } password)
        {
            var bytes = Encoding.ASCII.GetBytes($"{username}:{password}");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(bytes));
        }
    }

    private bool CanAttemptAutoReauth()
    {
        // Check if we have stored credentials that we can use for re-authentication
        var hasStoredCredentials = !string.IsNullOrEmpty(Preferences.Get("Username", ""))
            && !string.IsNullOrEmpty(Preferences.Get("ServerUrl", ""));

        // Check if current context matches stored credentials (avoid infinite loops)
        var storedUsername = Preferences.Get("Username", "");
        var contextMatches = _apiContext.Username == storedUsername;

        return hasStoredCredentials && contextMatches;
    }

    private async Task<bool> AttemptAutoReAuthentication()
    {
        try
        {
            // Get stored credentials
            var storedServerUrl = Preferences.Get("ServerUrl", "");
            var storedUsername = Preferences.Get("Username", "");

            if (string.IsNullOrEmpty(storedServerUrl) || string.IsNullOrEmpty(storedUsername))
            {
                return false;
            }

            // Note: Password is not persisted for security reasons
            // We'll use the current context password if it matches the stored username
            if (_apiContext.Username != storedUsername
                || string.IsNullOrEmpty(_apiContext.Password))
            {
                return false;
            }

            // Re-configure the context with stored server and current credentials
            if (!Uri.TryCreate(storedServerUrl, UriKind.Absolute, out var serverUri))
            {
                return false;
            }

            // Test if the current credentials are still valid by making a simple API call
            var testApiClient = CreateTestApiClient(
                serverUri,
                storedUsername,
                _apiContext.Password);

            var tags = await testApiClient.GetTags();

            // If we get here, re-authentication was successful
            _apiContext.Configure(serverUri, storedUsername, _apiContext.Password);
            _authService.SetAuthenticated(true);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"❌ [AutoRetryAuthHandler] Auto re-auth failed: {ex.Message}");

            return false;
        }
    }

    private IActivityApi CreateTestApiClient(Uri serverUri, string username, string password)
    {
        // Create a simple HTTP client with basic auth for testing
        var httpClient = new HttpClient();
        httpClient.BaseAddress = serverUri;

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            credentials);

        return Refit.RestService.For<IActivityApi>(httpClient);
    }

    private void ClearAuthenticationAndRedirect()
    {
        try
        {
            _authService.ClearAuthentication();
            _apiContext.Clear();

            // The MainLayout should automatically handle navigation to login when auth state changes
            System.Diagnostics.Debug.WriteLine(
                "[AutoRetryAuthHandler] Authentication cleared - MainLayout will handle navigation");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AutoRetryAuthHandler] Error clearing authentication: {ex.Message}");
        }
    }

    private static async Task<HttpRequestMessage> CloneRequest(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        // Copy headers (except Authorization which we'll add fresh)
        foreach (var header in original.Headers.Where(h => h.Key != "Authorization"))
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content if present
        if (original.Content != null)
        {
            var content = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            // Copy content headers
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
