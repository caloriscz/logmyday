namespace LogMyDay.Api.Application.Interfaces;

/// <summary>
/// A finished SQLite export. Content reads the database file, which is deleted once Content is
/// disposed. RowCounts holds the number of rows copied per table. UnknownMigrations lists the
/// applied migrations this version doesn't know; whatever they added is not in the file.
/// </summary>
public sealed record SqliteExport(Stream Content, string FileName, IReadOnlyDictionary<string, int> RowCounts, IReadOnlyList<string> UnknownMigrations) : IAsyncDisposable
{
    public int TotalRows => RowCounts.Values.Sum();

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public interface ISqliteExportService
{
    /// <summary>
    /// Copies the whole database, all users and every table, into a new SQLite file with this
    /// version's schema and migration history. LogMyDay runs on the file as it is. Works from
    /// either provider. Refuses a database that lacks some of this version's migrations.
    /// </summary>
    Task<SqliteExport> ExportDatabase(CancellationToken cancellationToken);
}
