namespace LogMyDay.Manager.Core.Services;

public interface IGitHubService
{
    Task<string> GetLatestVersionAsync(string owner = "caloriscz", string repo = "logmyday");
    Task<string> DownloadLatestReleaseAsync(string downloadPath, string owner = "caloriscz", string repo = "logmyday");
}
