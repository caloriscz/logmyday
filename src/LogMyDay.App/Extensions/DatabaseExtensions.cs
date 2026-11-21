using LogMyDay.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace LogMyDay.App.Extensions;

internal static class DatabaseExtensions
{
    internal static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddUserScopes();

        if (environment.EnvironmentName == "Test")
        {
            return services;
        }

        var dbOptions = configuration.GetSection(DatabaseProviderOptions.SectionName).Get<DatabaseProviderOptions>()
            ?? new DatabaseProviderOptions();
        dbOptions.EnsureSqlite();

        var connectionString = ResolveConnectionString(configuration, environment, dbOptions);
        DatabaseLocation.EnsureDirectory(connectionString);

        // The configured file is the platform database; each user's data is a file next to it.
        services.AddLogMyDayDatabases(connectionString);

        Log.Information("Database: SQLite, {Path}", DatabaseLocation.GetFilePath(connectionString) ?? connectionString);

        return services;
    }

    /// <summary>
    /// The connection string from the configuration, with an absolute path to the database file:
    /// Database:ConnectionString, then DB_PATH in Docker, then ConnectionStrings:DefaultConnection,
    /// then the default location in the application's folder.
    /// </summary>
    internal static string ResolveConnectionString(IConfiguration configuration, IWebHostEnvironment environment, DatabaseProviderOptions? dbOptions = null)
    {
        var configured = dbOptions?.ConnectionString;

        if (string.IsNullOrEmpty(configured) && environment.EnvironmentName == "Docker")
        {
            configured = $"Data Source={configuration["DB_PATH"] ?? "/app/data/logmyday.db"}";
        }

        if (string.IsNullOrEmpty(configured))
        {
            configured = configuration.GetConnectionString("DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(configured))
        {
            Log.Warning("No database connection string is configured; using the default location {Path} in the application's folder",
                DatabaseLocation.DefaultPath);
        }

        return DatabaseLocation.Resolve(configured, environment.ContentRootPath);
    }
}
