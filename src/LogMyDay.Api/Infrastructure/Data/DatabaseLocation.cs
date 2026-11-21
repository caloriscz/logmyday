using Microsoft.Data.Sqlite;

namespace LogMyDay.Api.Infrastructure.Data;

/// <summary>
/// Where the SQLite database file lives. Every installation states the place in its configuration;
/// this class only fills the gaps the same way everywhere: a default when nothing is configured,
/// and relative paths counted from the application's folder instead of the process's working
/// directory, which differs between hosts.
/// </summary>
public static class DatabaseLocation
{
    /// <summary>Used when no connection string is configured. Relative to the application's folder.</summary>
    public const string DefaultPath = "data/logmyday.db";

    /// <summary>
    /// Returns the connection string with an absolute path to the database file.
    /// </summary>
    /// <param name="connectionString">The configured connection string, or null/empty for the default location.</param>
    /// <param name="contentRoot">The application's folder.</param>
    public static string Resolve(string? connectionString, string contentRoot)
    {
        var builder = Parse(string.IsNullOrWhiteSpace(connectionString) ? $"Data Source={DefaultPath}" : connectionString);

        if (IsFile(builder.DataSource) && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRoot, builder.DataSource));
        }

        return builder.ToString();
    }

    /// <summary>
    /// The path of the database file, or null when the connection string doesn't name a plain
    /// file (in-memory database, "file:" URI).
    /// </summary>
    public static string? GetFilePath(string connectionString)
    {
        var dataSource = Parse(connectionString).DataSource;

        return IsFile(dataSource) ? dataSource : null;
    }

    /// <summary>Creates the folder of the database file. SQLite creates the file, not its folder.</summary>
    public static void EnsureDirectory(string connectionString)
    {
        var path = GetFilePath(connectionString);
        var directory = path is null ? null : Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static SqliteConnectionStringBuilder Parse(string connectionString)
    {
        try
        {
            return new SqliteConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException e)
        {
            // Typically a SQL Server connection string left over from an older version.
            throw new InvalidOperationException(
                $"The database connection string is not a SQLite connection string ({e.Message}). "
                + DatabaseProviderOptions.SqliteOnlyHelp, e);
        }
    }

    private static bool IsFile(string dataSource)
    {
        return !string.IsNullOrEmpty(dataSource)
            && !dataSource.StartsWith(':')
            && !dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            && !dataSource.StartsWith('|');
    }
}
