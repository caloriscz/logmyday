using System.Net.Http.Headers;

namespace LogMyDay.App.Mobile.Services;

public class ServerConfigurationService
{
    private readonly HttpClient _httpClient;
    private string _currentServerUrl = string.Empty;

    public ServerConfigurationService()
    {
        _httpClient = new HttpClient();
        LoadConfiguration();
    }

    public string CurrentServerUrl => _currentServerUrl;

    public void UpdateServerConfiguration(string serverUrl, string username, string password)
    {
        // Clean and validate server URL
        serverUrl = serverUrl?.Trim() ?? string.Empty;
        if (!serverUrl.StartsWith("http://") && !serverUrl.StartsWith("https://"))
        {
            serverUrl = "https://" + serverUrl;
        }
        serverUrl = serverUrl.TrimEnd('/');

        // Update HTTP client configuration
        _httpClient.BaseAddress = new Uri($"{serverUrl}/api/");
        
        // Set basic authentication
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);

        // Store configuration
        _currentServerUrl = serverUrl;
        Preferences.Set("ServerUrl", serverUrl);
        Preferences.Set("Username", username);
        Preferences.Set("Password", password);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<HttpResponseMessage> GetAsync(string endpoint)
    {
        return await _httpClient.GetAsync(endpoint);
    }

    public async Task<HttpResponseMessage> PostAsync(string endpoint, HttpContent content)
    {
        return await _httpClient.PostAsync(endpoint, content);
    }

    private void LoadConfiguration()
    {
        var serverUrl = Preferences.Get("ServerUrl", "https://logmyday.tadata.cz");
        var username = Preferences.Get("Username", "");
        var password = Preferences.Get("Password", "");

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            UpdateServerConfiguration(serverUrl, username, password);
        }
        else
        {
            _currentServerUrl = serverUrl;
            _httpClient.BaseAddress = new Uri($"{serverUrl}/api/");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
