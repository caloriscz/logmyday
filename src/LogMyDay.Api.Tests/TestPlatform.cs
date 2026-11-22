using System.Runtime.CompilerServices;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

/// <summary>
/// The platform side (users, API keys, settings) of a test's data context: a
/// <see cref="PlatformDbContext"/> over the same in-memory store or SQLite database. Shared by
/// the test projects, hence no namespace.
/// </summary>
internal static class TestPlatform
{
    private static readonly ConditionalWeakTable<LogMyDayDbContext, PlatformDbContext> Contexts = new();

    /// <summary>
    /// The platform context that goes with a data context. One per data context, and saved
    /// whenever the data context saves, so a test can add its user next to its data as before.
    /// </summary>
    public static PlatformDbContext For(LogMyDayDbContext db)
    {
        return Contexts.GetValue(db, Create);
    }

    /// <summary>The user directory the data services read users from.</summary>
    public static IUserDirectory Users(LogMyDayDbContext db)
    {
        return new UserDirectory(For(db));
    }

    private static PlatformDbContext Create(LogMyDayDbContext db)
    {
        // The same options as the data context (provider, store or connection, warnings), so both
        // contexts reach the same database.
        var builder = new DbContextOptionsBuilder<PlatformDbContext>();
        foreach (var extension in db.GetService<IDbContextOptions>().Extensions)
        {
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension((dynamic)extension);
        }

        var platform = new PlatformDbContext(builder.Options);
        if (db.Database.IsRelational())
        {
            CreateTables(db, platform);
        }

        db.SavingChanges += (_, _) => platform.SaveChanges();

        return platform;
    }

    /// <summary>
    /// A test's SQLite database is built from the data model (EnsureCreated). The platform tables
    /// follow here; Settings belongs to both models and exists already.
    /// </summary>
    private static void CreateTables(LogMyDayDbContext db, PlatformDbContext platform)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var check = connection.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('LogMyDay_Users', 'LogMyDay_Activities');";
            var existing = Convert.ToInt32(check.ExecuteScalar());

            // 0: nothing is created yet, the test will do it. 2: both models are there already.
            if (existing != 1)
            {
                return;
            }

            foreach (var statement in platform.Database.GenerateCreateScript().Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!statement.Contains("LogMyDay_Settings", StringComparison.Ordinal))
                {
                    using var create = connection.CreateCommand();
                    create.CommandText = statement + ";";
                    create.ExecuteNonQuery();
                }
            }
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }
}
