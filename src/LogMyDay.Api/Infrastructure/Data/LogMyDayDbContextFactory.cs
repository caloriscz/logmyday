namespace LogMyDay.Api.Infrastructure.Data;

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Design-time factory for LogMyDayDbContext to enable EF Core migrations.
/// </summary>
public class LogMyDayDbContextFactory : IDesignTimeDbContextFactory<LogMyDayDbContext>
{
    public LogMyDayDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        // Try to find the LogMyDay.App project folder to load configuration
        var currentDir = Directory.GetCurrentDirectory();
        Debug.WriteLine($"[LogMyDayDbContextFactory] Current Directory: {currentDir}");
        Debug.WriteLine($"[LogMyDayDbContextFactory] Environment: {environmentName}");

        var appProjectDir = FindAppProjectDirectory(currentDir);
        Debug.WriteLine($"[LogMyDayDbContextFactory] App Project Directory resolved to: {appProjectDir ?? "Not Found"}");

        if (appProjectDir == null)
        {
             Debug.WriteLine("[LogMyDayDbContextFactory] WARNING: Could not find LogMyDay.App directory. Configuration might not be loaded.");
        }

        var builder = new ConfigurationBuilder()
            .SetBasePath(appProjectDir ?? currentDir)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables();

        var configuration = builder.Build();

        var dbOptions = configuration.GetSection(DatabaseProviderOptions.SectionName).Get<DatabaseProviderOptions>()
            ?? new DatabaseProviderOptions();
        dbOptions.EnsureSqlite();

        var configured = !string.IsNullOrEmpty(dbOptions.ConnectionString)
            ? dbOptions.ConnectionString
            : configuration.GetConnectionString("DefaultConnection");

        // The same file the app uses: relative paths count from the LogMyDay.App folder.
        var connectionString = DatabaseLocation.Resolve(configured, appProjectDir ?? currentDir);
        DatabaseLocation.EnsureDirectory(connectionString);

        Debug.WriteLine($"[LogMyDayDbContextFactory] Database: {DatabaseLocation.GetFilePath(connectionString) ?? connectionString}");

        var optionsBuilder = new DbContextOptionsBuilder<LogMyDayDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new LogMyDayDbContext(optionsBuilder.Options);
    }

    private string? FindAppProjectDirectory(string startPath)
    {
        // Common paths relative to where dotnet ef might be run
        var candidates = new[]
        {
            Path.Combine(startPath, "src", "LogMyDay.App"),
            Path.Combine(startPath, "..", "LogMyDay.App"),
            Path.Combine(startPath, "LogMyDay.App")
        };

        foreach (var path in candidates)
        {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "appsettings.json")))
            {
                return path;
            }
        }

        return null;
    }
}
