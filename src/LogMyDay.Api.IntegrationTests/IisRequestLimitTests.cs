using System.Net.Http.Headers;
using System.Xml.Linq;
using LogMyDay.Api.Controllers;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// On IIS, request filtering refuses larger requests before the app sees them. The web.config
/// that publishing puts next to the app lets a restore of the largest database file through.
/// </summary>
public class IisRequestLimitTests
{
    [Fact]
    public void TheWebConfig_LetsTheLargestDatabaseRestoreThrough()
    {
        var webConfig = XDocument.Load(Path.Combine(SourceFolder(), "LogMyDay.App", "web.config"));

        var limit = webConfig.Descendants("requestLimits").Single().Attribute("maxAllowedContentLength")!.Value;

        Assert.Equal(BackupController.MaxDatabaseRequestBytes, long.Parse(limit));
    }

    [Fact]
    public void TheWebConfig_IsNotInheritedByApplicationsNestedUnderTheSite()
    {
        var webConfig = XDocument.Load(Path.Combine(SourceFolder(), "LogMyDay.App", "web.config"));

        var location = Assert.Single(webConfig.Root!.Elements("location"));
        Assert.Equal(".", location.Attribute("path")?.Value);
        Assert.Equal("false", location.Attribute("inheritInChildApplications")?.Value);
        Assert.Empty(webConfig.Root.Elements("system.webServer"));
    }

    [Fact]
    public void AFileOfTheLargestSize_FitsInARestoreRequest_WithTheMultipartFormAroundIt()
    {
        var file = new ByteArrayContent(new byte[10]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var form = new MultipartFormDataContent { { file, "file", new string('n', 255) + ".db" } };

        var overhead = form.Headers.ContentLength!.Value - 10;

        Assert.True(BackupController.MaxDatabaseUploadBytes + overhead <= BackupController.MaxDatabaseRequestBytes);
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
