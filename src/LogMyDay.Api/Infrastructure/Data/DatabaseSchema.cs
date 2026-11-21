using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace LogMyDay.Api.Infrastructure.Data;

/// <summary>
/// Creates LogMyDay's schema in a new, empty database. The oldest migrations were scaffolded for
/// SQL Server and fail on SQLite, so a new database gets this version's schema from the model,
/// with every migration recorded as applied. Later migrations then apply to it as usual.
/// </summary>
public static class DatabaseSchema
{
    /// <summary>
    /// Creates the schema when the database is missing or has no tables, and returns whether it
    /// did. A database that already has tables is left to the migrations.
    /// </summary>
    public static async Task<bool> CreateIfEmpty(LogMyDayDbContext context, PlatformDbContext platform, CancellationToken cancellationToken = default)
    {
        var creator = context.GetService<IRelationalDatabaseCreator>();
        if (await creator.ExistsAsync(cancellationToken) && await creator.HasTablesAsync(cancellationToken))
        {
            return false;
        }

        await context.Database.EnsureCreatedAsync(cancellationToken);

        // Both models still share one file, and the data context has just created it. The
        // platform tables follow; Settings belongs to both models and exists already.
        foreach (var statement in platform.Database.GenerateCreateScript().Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!statement.Contains("LogMyDay_Settings", StringComparison.Ordinal))
            {
                await context.Database.ExecuteSqlRawAsync(statement + ";", cancellationToken);
            }
        }

        var history = context.GetService<IHistoryRepository>();
        await context.Database.ExecuteSqlRawAsync(history.GetCreateScript(), cancellationToken);

        var productVersion = ProductInfo.GetVersion();
        foreach (var migration in context.Database.GetMigrations())
        {
            await context.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(migration, productVersion)), cancellationToken);
        }

        return true;
    }
}
