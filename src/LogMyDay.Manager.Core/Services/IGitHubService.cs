namespace LogMyDay.Manager.Core.Services;

public interface IGitHubService
{
    Task<string> GetLatestVersionAsync(string owner = "yourusername", string repo = "logmyday");
    Task<string> DownloadLatestReleaseAsync(string downloadPath, string owner = "yourusername", string repo = "logmyday");
}
