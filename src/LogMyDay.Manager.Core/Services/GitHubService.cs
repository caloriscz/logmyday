using System.IO.Compression;
using System.Text.Json;

namespace LogMyDay.Manager.Core.Services;

public class GitHubService : IGitHubService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GitHubService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetLatestVersionAsync(string owner = "caloriscz", string repo = "logmyday")
    {
        var client = _httpClientFactory.CreateClient("GitHub");
        var response = await client.GetAsync($"/repos/{owner}/{repo}/releases/latest");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var tagName = document.RootElement.GetProperty("tag_name").GetString();

        return tagName ?? "unknown";
    }

    public async Task<string> DownloadLatestReleaseAsync(string downloadPath, string owner = "caloriscz", string repo = "logmyday")
    {
        var client = _httpClientFactory.CreateClient("GitHub");
        var response = await client.GetAsync($"/repos/{owner}/{repo}/releases/latest");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);
        var assets = document.RootElement.GetProperty("assets");

        string? downloadUrl = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name != null && (name.EndsWith(".zip") || name.Contains("LogMyDay")))
            {
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new Exception("No suitable release asset found");
        }

        // Download the file
        var zipPath = Path.Combine(downloadPath, "logmyday-release.zip");
        Directory.CreateDirectory(downloadPath);

        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LogMyDay-Manager/1.0");
            var zipBytes = await httpClient.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipPath, zipBytes);
        }

        // Extract the zip file
        var extractPath = Path.Combine(downloadPath, "extracted");
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }
        ZipFile.ExtractToDirectory(zipPath, extractPath);

        return extractPath;
    }
}
