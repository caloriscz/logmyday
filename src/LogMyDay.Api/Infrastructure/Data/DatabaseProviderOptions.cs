namespace LogMyDay.Api.Infrastructure.Data;

/// <summary>
/// Configuration model for database provider selection.
/// </summary>
public class DatabaseProviderOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// The database provider to use. Defaults to SqlServer.
    /// </summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    /// <summary>
    /// Optional connection string override. When set, takes precedence over ConnectionStrings:DefaultConnection.
    /// </summary>
    public string? ConnectionString { get; set; }
}
