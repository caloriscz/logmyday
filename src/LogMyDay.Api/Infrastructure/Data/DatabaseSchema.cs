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
    public static async Task<bool> CreateIfEmpty(LogMyDayDbContext context, CancellationToken cancellationToken = default)
    {
        var creator = context.GetService<IRelationalDatabaseCreator>();
        if (await creator.ExistsAsync(cancellationToken) && await creator.HasTablesAsync(cancellationToken))
        {
            return false;
        }

        await context.Database.EnsureCreatedAsync(cancellationToken);

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
