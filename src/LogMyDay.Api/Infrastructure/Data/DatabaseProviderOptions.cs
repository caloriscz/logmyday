namespace LogMyDay.Api.Infrastructure.Data;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Configuration of the database (section "Database"). LogMyDay runs on SQLite only.
/// </summary>
public class DatabaseProviderOptions
{
    public const string SectionName = "Database";

    /// <summary>How to move an installation that is still configured for SQL Server.</summary>
    public const string SqliteOnlyHelp =
        "Move the data with the SQLite export of a version that still supports SQL Server "
        + "(Backup page, Export Whole Database), point ConnectionStrings:DefaultConnection at the "
        + "exported file (\"Data Source=<path>\") and remove Database:Provider.";

    /// <summary>
    /// Optional, and "Sqlite" if set. Kept so that a configuration still asking for SQL Server
    /// stops the app with an explanation instead of a connection-string error.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Optional connection string override. When set, takes precedence over ConnectionStrings:DefaultConnection.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>How to move a Docker installation that still has the SQL Server settings of v0.7.2.</summary>
    public const string DockerSqliteOnlyHelp =
        "Move the data with the SQLite export of a version that still supports SQL Server "
        + "(Backup page, Export Whole Database), put the exported file at DB_PATH on the logmyday-data "
        + "volume (/app/data/logmyday.db by default), and remove DB_HOST, DB_NAME, DB_USER and the "
        + "db_password secret.";

    /// <summary>The settings that LogMyDay up to v0.7.2 read in Docker to reach SQL Server.</summary>
    public static readonly IReadOnlyList<string> DockerSqlServerSettings = ["DB_HOST", "DB_NAME", "DB_USER", "db_password"];

    /// <summary>Throws when the configuration names a provider other than SQLite.</summary>
    public void EnsureSqlite()
    {
        if (!string.IsNullOrEmpty(Provider) && !string.Equals(Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Database:Provider is \"{Provider}\", but LogMyDay runs on SQLite only. " + SqliteOnlyHelp);
        }
    }

    /// <summary>
    /// Checks a Docker installation for the SQL Server settings of v0.7.2. With them and without a
    /// SQLite database, the start would create an empty installation instead of using the data in
    /// SQL Server, so it throws. Once the SQLite database exists, the settings are only leftovers.
    /// </summary>
    /// <param name="databasePath">The SQLite database file the installation is configured for.</param>
    /// <returns>The SQL Server settings found, which are ignored.</returns>
    public static IReadOnlyList<string> CheckDockerSqlServerSettings(IConfiguration configuration, string? databasePath)
    {
        var found = DockerSqlServerSettings.Where(name => !string.IsNullOrWhiteSpace(configuration[name])).ToList();
        if (found.Count > 0 && (databasePath is null || !File.Exists(databasePath)))
        {
            throw new InvalidOperationException(
                $"The Docker configuration still has the SQL Server settings of LogMyDay v0.7.2 ({string.Join(", ", found)}), "
                + $"and there is no SQLite database at {databasePath ?? "the configured location"}. LogMyDay runs on SQLite only; "
                + "starting now would create an empty installation. " + DockerSqliteOnlyHelp);
        }

        return found;
    }
}
