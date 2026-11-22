using LogMyDay.App.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// A Docker installation of v0.7.2 reached SQL Server through DB_HOST, DB_NAME, DB_USER and the
/// db_password secret. The app's database registration stops such a start instead of creating an
/// empty SQLite installation next to the data.
/// </summary>
public sealed class DockerDatabaseConfigurationTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"lmd-docker-config-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string DatabasePath => Path.Combine(_folder, "data", "logmyday.db");

    [Fact]
    public void ADockerStart_WithTheSqlServerSettingsOfV072_AndNoSqliteDatabase_Stops()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Register("Docker", ("DB_HOST", "host.docker.internal,1439"), ("db_password", "secret")));

        Assert.Contains("DB_HOST, db_password", error.Message);
        Assert.False(File.Exists(DatabasePath), "No empty installation is created.");
    }

    [Fact]
    public void ADockerStart_OnAnExistingSqliteDatabase_IgnoresTheLeftoverSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        File.WriteAllBytes(DatabasePath, []);

        Register("Docker", ("DB_HOST", "host.docker.internal,1439"), ("db_password", "secret"));
    }

    [Fact]
    public void OutsideDocker_TheSettingsWereNeverRead_AndStopNothing()
    {
        Register("Production", ("DB_HOST", "somewhere"), ("ConnectionStrings:DefaultConnection", $"Data Source={DatabasePath}"));
    }

    private void Register(string environmentName, params (string Name, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Name, s.Value))
                .Append(new KeyValuePair<string, string?>("DB_PATH", DatabasePath)))
            .Build();

        new ServiceCollection().AddDatabase(configuration, new Environment(environmentName, _folder));
    }

    private sealed class Environment(string name, string contentRoot) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "LogMyDay.App";

        public string ContentRootPath { get; set; } = contentRoot;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = contentRoot;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
