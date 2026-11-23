using System.Xml.Linq;
using LogMyDay.Api.Controllers;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// On IIS, request filtering refuses larger requests before the app sees them. The web.config
/// that publishing puts next to the app lets a database file of the size the restore takes through.
/// </summary>
public class IisRequestLimitTests
{
    [Fact]
    public void TheWebConfig_LetsTheLargestDatabaseRestoreThrough()
    {
        var webConfig = XDocument.Load(Path.Combine(SourceFolder(), "LogMyDay.App", "web.config"));

        var limit = webConfig.Descendants("requestLimits").Single().Attribute("maxAllowedContentLength")!.Value;

        Assert.Equal(BackupController.MaxDatabaseUploadBytes, long.Parse(limit));
    }

    private static string SourceFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "LogMyDay.sln")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("The test runs outside the source tree: LogMyDay.sln was not found.");
    }
}
