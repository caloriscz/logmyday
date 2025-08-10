using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LogMyDay.App.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://logmyday.tadata.cz";

    public ApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<bool> SaveLinkAsync(string url, string text)
    {
        try
        {
            // Simple implementation - just return true for now to avoid crashes
            await Task.Delay(1000);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
