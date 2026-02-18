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

        services.AddDbContext<LogMyDayDbContext>(options =>
        {
            options.UseSqlServer(ResolveConnectionString(configuration, environment));
        });

        return services;
    }

    internal static string ResolveConnectionString(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (environment.EnvironmentName == "Docker")
        {
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
