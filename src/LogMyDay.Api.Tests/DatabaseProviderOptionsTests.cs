using LogMyDay.Api.Infrastructure.Data;

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
}
