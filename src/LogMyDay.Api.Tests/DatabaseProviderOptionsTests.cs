using LogMyDay.Api.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace LogMyDay.Api.Tests;

/// <summary>
/// LogMyDay runs on SQLite only. A configuration that still asks for SQL Server stops the app
/// with an explanation instead of failing later on a SQL Server connection string.
/// </summary>
public class DatabaseProviderOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Sqlite")]
    [InlineData("sqlite")]
    public void EnsureSqlite_AcceptsNoProviderOrSqlite(string? provider)
    {
        new DatabaseProviderOptions { Provider = provider }.EnsureSqlite();
    }

    [Fact]
    public void EnsureSqlite_RefusesSqlServerAndSaysHowToMove()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new DatabaseProviderOptions { Provider = "SqlServer" }.EnsureSqlite());

        Assert.Contains("SQLite only", error.Message);
        Assert.Contains("Export Whole Database", error.Message);
    }

    [Theory]
    [InlineData("DB_HOST", "host.docker.internal,1439")]
    [InlineData("DB_NAME", "logmyday")]
    [InlineData("DB_USER", "sa")]
    [InlineData("db_password", "from the Docker secret")]
    public void ASqlServerSettingOfV072_WithoutASqliteDatabase_StopsTheStartAndSaysHowToMove(string name, string value)
    {
        var missing = Path.Combine(Path.GetTempPath(), $"lmd-no-db-{Guid.NewGuid():N}", "logmyday.db");

        var error = Assert.Throws<InvalidOperationException>(
            () => DatabaseProviderOptions.CheckDockerSqlServerSettings(Configuration((name, value)), missing));

        Assert.Contains(name, error.Message);
        Assert.Contains(missing, error.Message);
        Assert.Contains("Export Whole Database", error.Message);
        Assert.Contains("DB_PATH", error.Message);
    }

    [Fact]
    public void TheSqlServerSettings_OnceTheSqliteDatabaseExists_AreLeftoversOnly()
    {
        var folder = NewFolder();
        try
        {
            var database = Path.Combine(folder, "logmyday.db");
            File.WriteAllText(database, "the data");
            var configuration = Configuration(("DB_HOST", "host.docker.internal,1439"), ("DB_USER", "sa"), ("db_password", "secret"));

            Assert.Equal(["DB_HOST", "DB_USER", "db_password"], DatabaseProviderOptions.CheckDockerSqlServerSettings(configuration, database));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnEmptyDatabaseFile_IsNoData_AndStopsTheStart()
    {
        var folder = NewFolder();
        try
        {
            var database = Path.Combine(folder, "logmyday.db");
            File.WriteAllBytes(database, []);

            Assert.Throws<InvalidOperationException>(
                () => DatabaseProviderOptions.CheckDockerSqlServerSettings(Configuration(("db_password", "secret")), database));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AfterAnInterruptedUpgrade_TheBackupCountsAsData_SoTheSplitCanSayHowToRecover()
    {
        var folder = NewFolder();
        try
        {
            var database = Path.Combine(folder, "logmyday.db");
            File.WriteAllText(Path.Combine(folder, "logmyday.before-v0.8.0.db"), "the original");

            Assert.Equal(["DB_HOST"], DatabaseProviderOptions.CheckDockerSqlServerSettings(Configuration(("DB_HOST", "sql")), database));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ALocationThatIsNotAFile_IsNotChecked()
    {
        Assert.Equal(["DB_HOST"], DatabaseProviderOptions.CheckDockerSqlServerSettings(Configuration(("DB_HOST", "sql")), databasePath: null));
    }

    [Fact]
    public void ANewDockerInstallation_WithoutTheSqlServerSettings_Starts()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"lmd-no-db-{Guid.NewGuid():N}", "logmyday.db");

        Assert.Empty(DatabaseProviderOptions.CheckDockerSqlServerSettings(Configuration(("DB_PATH", missing), ("DB_HOST", " ")), missing));
    }

    private static string NewFolder()
    {
        return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"lmd-docker-db-{Guid.NewGuid():N}")).FullName;
    }

    private static IConfiguration Configuration(params (string Name, string Value)[] settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Name, s.Value)))
            .Build();
    }
}
