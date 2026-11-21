using LogMyDay.Api.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace LogMyDay.Api.Tests;

/// <summary>
/// The database file's place comes from the configuration. Without one it is data/logmyday.db in
/// the application's folder, and a relative path counts from that folder on every host.
/// </summary>
public class DatabaseLocationTests
{
    private static readonly string ContentRoot = Path.Combine(Path.GetTempPath(), "lmd-content-root");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_WithoutConnectionString_UsesTheDefaultInTheApplicationFolder(string? connectionString)
    {
        var resolved = DatabaseLocation.Resolve(connectionString, ContentRoot);

        Assert.Equal(Path.Combine(ContentRoot, "data", "logmyday.db"), DatabaseLocation.GetFilePath(resolved));
    }

    [Fact]
    public void Resolve_RelativePath_CountsFromTheApplicationFolder()
    {
        var resolved = DatabaseLocation.Resolve("Data Source=logmyday.db", ContentRoot);

        Assert.Equal(Path.Combine(ContentRoot, "logmyday.db"), DatabaseLocation.GetFilePath(resolved));
    }

    [Fact]
    public void Resolve_AbsolutePath_IsKept()
    {
        var path = Path.Combine(Path.GetTempPath(), "elsewhere", "my.db");

        var resolved = DatabaseLocation.Resolve($"Data Source={path}", ContentRoot);

        Assert.Equal(path, DatabaseLocation.GetFilePath(resolved));
    }

    [Fact]
    public void Resolve_KeepsTheOtherSettings()
    {
        var resolved = new SqliteConnectionStringBuilder(
            DatabaseLocation.Resolve("Data Source=logmyday.db;Foreign Keys=False;Default Timeout=45", ContentRoot));

        Assert.False(resolved.ForeignKeys);
        Assert.Equal(45, resolved.DefaultTimeout);
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=file:shared?mode=memory&cache=shared")]
    public void Resolve_NotAPlainFile_IsLeftAlone(string connectionString)
    {
        var resolved = DatabaseLocation.Resolve(connectionString, ContentRoot);

        Assert.Null(DatabaseLocation.GetFilePath(resolved));
        Assert.Equal(new SqliteConnectionStringBuilder(connectionString).DataSource, new SqliteConnectionStringBuilder(resolved).DataSource);
    }

    [Fact]
    public void Resolve_SqlServerConnectionString_SaysHowToMove()
    {
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseLocation.Resolve(
            "server=host,1433;user=me;password=secret;database=lmd;Encrypt=True;TrustServerCertificate=False", ContentRoot));

        Assert.Contains("not a SQLite connection string", error.Message);
        Assert.Contains("Export Whole Database", error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Fact]
    public void EnsureDirectory_CreatesTheFolderOfTheFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lmd-location-{Guid.NewGuid():N}");
        try
        {
            var resolved = DatabaseLocation.Resolve(null, root);

            DatabaseLocation.EnsureDirectory(resolved);

            Assert.True(Directory.Exists(Path.Combine(root, "data")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureDirectory_InMemory_DoesNothing()
    {
        DatabaseLocation.EnsureDirectory("Data Source=:memory:");
    }
}
