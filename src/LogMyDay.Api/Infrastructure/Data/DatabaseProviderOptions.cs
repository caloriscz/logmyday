namespace LogMyDay.Api.Infrastructure.Data;

/// <summary>
/// Configuration of the database (section "Database"). LogMyDay runs on SQLite only.
/// </summary>
public class DatabaseProviderOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Optional, and "Sqlite" if set. Kept so that a configuration still asking for SQL Server
    /// stops the app with an explanation instead of a connection-string error.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Optional connection string override. When set, takes precedence over ConnectionStrings:DefaultConnection.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Throws when the configuration names a provider other than SQLite.</summary>
    public void EnsureSqlite()
    {
        if (!string.IsNullOrEmpty(Provider) && !string.Equals(Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Database:Provider is \"{Provider}\", but LogMyDay runs on SQLite only. "
                + "Move the data with the SQLite export of a version that still supports SQL Server "
                + "(Backup page, Export Whole Database), point ConnectionStrings:DefaultConnection at the "
                + "exported file (\"Data Source=<path>\") and remove Database:Provider.");
        }
    }
}
