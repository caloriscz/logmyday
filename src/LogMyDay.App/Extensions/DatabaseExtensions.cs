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

        var connectionString = ResolveConnectionString(configuration, environment, dbOptions);

        services.AddDbContext<LogMyDayDbContext>(options =>
        {
            ConfigureProvider(options, dbOptions.Provider, connectionString);
        });

        Log.Information("Database provider configured: {Provider}", dbOptions.Provider);

        return services;
    }

    internal static void ConfigureProvider(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                options.UseSqlite(connectionString);
                break;

            case DatabaseProvider.SqlServer:
            default:
                options.UseSqlServer(connectionString);
                break;
        }
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
            var provider = dbOptions?.Provider ?? DatabaseProvider.SqlServer;

            if (provider == DatabaseProvider.Sqlite)
            {
                var dbPath = configuration["DB_PATH"] ?? "/app/data/logmyday.db";

                return $"Data Source={dbPath}";
            }

            var dbPassword = configuration["db_password"];
            var dbHost = configuration["DB_HOST"] ?? "host.docker.internal,1439";
            var dbName = configuration["DB_NAME"] ?? "logmyday";
            var dbUser = configuration["DB_USER"] ?? "sa";

            return $"Server={dbHost};Database={dbName};User Id={dbUser};Password={dbPassword};Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;";
        }

        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }
}
