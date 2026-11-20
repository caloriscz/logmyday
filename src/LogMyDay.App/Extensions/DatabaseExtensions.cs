using LogMyDay.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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

        services.AddDbContext<LogMyDayDbContext>(options => options.UseSqlite(connectionString));

        Log.Information("Database provider configured: Sqlite");

        return services;
    }

    internal static string ResolveConnectionString(IConfiguration configuration, IWebHostEnvironment environment, DatabaseProviderOptions? dbOptions = null)
    {
        // If Database:ConnectionString is explicitly set, use it
        if (!string.IsNullOrEmpty(dbOptions?.ConnectionString))
        {
            return dbOptions.ConnectionString;
        }

        if (environment.EnvironmentName == "Docker")
        {
            var dbPath = configuration["DB_PATH"] ?? "/app/data/logmyday.db";

            return $"Data Source={dbPath}";
        }

        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }
}
