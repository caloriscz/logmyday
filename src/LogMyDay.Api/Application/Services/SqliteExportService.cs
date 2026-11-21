using System.Data.Common;
using System.Reflection;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// Exports the whole database into a new SQLite file. The file gets this version's schema, with
/// every migration recorded as applied, so LogMyDay runs on it and applies later migrations as
/// usual. It is built the way a new installation's database is (DatabaseSchema), and the rows are
/// then copied from the current database.
/// </summary>
public sealed class SqliteExportService : ISqliteExportService
{
    private static readonly MethodInfo CopyTableMethod = typeof(SqliteExportService)
        .GetMethod(nameof(CopyTable), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly LogMyDayDbContext _context;
    private readonly PlatformDbContext _platform;
    private readonly ILogger<SqliteExportService> _logger;

    public SqliteExportService(LogMyDayDbContext context, PlatformDbContext platform, ILogger<SqliteExportService> logger)
    {
        _context = context;
        _platform = platform;
        _logger = logger;
    }

    public async Task<SqliteExport> ExportDatabase(CancellationToken cancellationToken)
    {
        var unknownMigrations = await CheckMigrations(cancellationToken);

        var path = Path.Combine(Path.GetTempPath(), $"logmyday-export-{Guid.NewGuid():N}.db");
        IReadOnlyDictionary<string, int> rowCounts;
        try
        {
            rowCounts = await WriteDatabase(path, cancellationToken);
        }
        catch
        {
            // A partial file is of no use to anyone; don't leave it in the temp folder.
            File.Delete(path);

            throw;
        }

        var content = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        var export = new SqliteExport(content, $"logmyday-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.db", rowCounts, unknownMigrations);

        _logger.LogInformation("Exported the database to SQLite: {Rows} rows in {Tables} tables",
            export.TotalRows, rowCounts.Count);

        return export;
    }

    /// <summary>
    /// The copy maps the rows onto this version's schema. A database that lacks some of this
    /// version's migrations is refused: its columns don't match. Applied migrations this version
    /// doesn't know (one whose file was removed, or one from another branch) are returned: they may
    /// have added tables or columns, which the copy leaves out.
    /// </summary>
    private async Task<IReadOnlyList<string>> CheckMigrations(CancellationToken cancellationToken)
    {
        var applied = (await _context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var known = _context.Database.GetMigrations().ToList();

        var missing = known.Except(applied).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"The database lacks migrations of this version, so it can't be exported: {string.Join(", ", missing)}. Update the database first.");
        }

        var unknown = applied.Except(known).ToList();
        if (unknown.Count > 0)
        {
            _logger.LogWarning("The SQLite export leaves out whatever these unknown migrations added: {Migrations}",
                string.Join(", ", unknown));
        }

        return unknown;
    }

    private async Task<IReadOnlyDictionary<string, int>> WriteDatabase(string path, CancellationToken cancellationToken)
    {
        // No pooling: the file has to be free to read and delete as soon as the connection closes.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var target = new LogMyDayDbContext(options);
        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var targetPlatform = new PlatformDbContext(platformOptions);
        await DatabaseSchema.CreateIfEmpty(target, targetPlatform, cancellationToken);

        // Rows keep their keys and come table by table, and some tables point at each other
        // (Quantity and Unit, Tag and its record type), so no table order satisfies every key.
        // The source keeps its references whole. SQLite ignores this pragma inside a transaction.
        await Execute(connection, null, "PRAGMA foreign_keys = OFF;", cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // The users' data and the platform tables go into the one file. Settings belongs to both
        // models and is one table, so it is copied once.
        var tables = target.Model.GetEntityTypes().Select(t => (Source: (DbContext)_context, EntityType: t))
            .Concat(targetPlatform.Model.GetEntityTypes().Select(t => (Source: (DbContext)_platform, EntityType: t)))
            .DistinctBy(t => t.EntityType.GetTableName())
            .ToList();

        // The migrations seed reference rows (HasData). The copy brings the source's rows instead.
        foreach (var table in tables)
        {
            await Execute(connection, transaction, $"DELETE FROM \"{table.EntityType.GetTableName()}\";", cancellationToken);
        }

        var rowCounts = new Dictionary<string, int>();
        foreach (var table in tables)
        {
            var copy = (Task<int>)CopyTableMethod.MakeGenericMethod(table.EntityType.ClrType)
                .Invoke(this, [table.Source, connection, transaction, table.EntityType, cancellationToken])!;
            rowCounts[table.EntityType.GetTableName()!] = await copy;
        }

        await transaction.CommitAsync(cancellationToken);

        return rowCounts;
    }

    private async Task<int> CopyTable<TEntity>(DbContext source, DbConnection connection, DbTransaction transaction, IEntityType entityType, CancellationToken cancellationToken)
        where TEntity : class
    {
        var properties = entityType.GetProperties().ToList();

        // Values are read from the entity objects, so a column with no CLR property would be lost.
        var shadow = properties.FirstOrDefault(p => p.IsShadowProperty());
        if (shadow != null)
        {
            throw new NotSupportedException($"The SQLite export can't copy the shadow property {entityType.DisplayName()}.{shadow.Name}.");
        }

        var getters = properties.Select(p => p.GetGetter()).ToList();
        var mappings = properties.Select(p => p.GetRelationalTypeMapping()).ToList();
        var columns = string.Join(", ", properties.Select(p => $"\"{p.GetColumnName()}\""));
        var parameters = string.Join(", ", properties.Select((_, i) => $"@p{i}"));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO \"{entityType.GetTableName()}\" ({columns}) VALUES ({parameters});";

        var count = 0;
        var rows = source.Set<TEntity>().AsNoTracking().AsAsyncEnumerable();
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            command.Parameters.Clear();
            for (var i = 0; i < properties.Count; i++)
            {
                // The mapping converts the value as EF does: enums to numbers, Guids and dates to text.
                var value = getters[i].GetClrValueUsingContainingEntity(row);
                command.Parameters.Add(mappings[i].CreateParameter(command, $"@p{i}", value, properties[i].IsNullable));
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
            count++;
        }

        return count;
    }

    private static async Task Execute(DbConnection connection, DbTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
