using LogMyDay.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;

namespace LogMyDay.App.Extensions;

internal static class DatabaseExtensions
{
    internal static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (environment.EnvironmentName == "Test")
        {
            return services;
        }

        var dbOptions = configuration.GetSection(DatabaseProviderOptions.SectionName).Get<DatabaseProviderOptions>()
            ?? new DatabaseProviderOptions();
        dbOptions.EnsureSqlite();

        var connectionString = ResolveConnectionString(configuration, environment, dbOptions);
        DatabaseLocation.EnsureDirectory(connectionString);

        // The model no longer matches the last migration: the platform tables left it. The new
        // migration baseline brings the check back.
        services.AddDbContext<LogMyDayDbContext>(options => options
            .UseSqlite(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        // The platform tables still live in the same file as the users' data.
        services.AddDbContext<PlatformDbContext>(options => options.UseSqlite(connectionString));

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
