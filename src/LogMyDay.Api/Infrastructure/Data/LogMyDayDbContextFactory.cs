namespace LogMyDay.Api.Infrastructure.Data;

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
        // Try to find the LogMyDay.App project folder to load configuration
        var currentDir = Directory.GetCurrentDirectory();
        Console.WriteLine($"[LogMyDayDbContextFactory] Current Directory: {currentDir}");

        var appProjectDir = FindAppProjectDirectory(currentDir);
        Console.WriteLine($"[LogMyDayDbContextFactory] App Project Directory resolved to: {appProjectDir ?? "Not Found"}");

        if (appProjectDir == null)
        {
             Console.WriteLine("[LogMyDayDbContextFactory] WARNING: Could not find LogMyDay.App directory. Configuration might not be loaded.");
        }

        var builder = new ConfigurationBuilder()
            .SetBasePath(appProjectDir ?? currentDir)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true);

        var configuration = builder.Build();

        var dbOptions = configuration.GetSection(DatabaseProviderOptions.SectionName).Get<DatabaseProviderOptions>()
            ?? new DatabaseProviderOptions();

        var connectionString = !string.IsNullOrEmpty(dbOptions.ConnectionString)
            ? dbOptions.ConnectionString
            : configuration.GetConnectionString("DefaultConnection");

        Console.WriteLine($"[LogMyDayDbContextFactory] Provider: {dbOptions.Provider}");
        Console.WriteLine($"[LogMyDayDbContextFactory] Connection String found: {(string.IsNullOrEmpty(connectionString) ? "NO" : "YES")}");

        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("[LogMyDayDbContextFactory] ERROR: Connection string not found.");
            Console.WriteLine("[LogMyDayDbContextFactory] Please ensure appsettings.json or appsettings.Development.json exists in LogMyDay.App and contains the connection string.");

            throw new InvalidOperationException("Could not find a database connection string.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<LogMyDayDbContext>();

        switch (dbOptions.Provider)
        {
            case DatabaseProvider.Sqlite:
                optionsBuilder.UseSqlite(connectionString);
                break;

            case DatabaseProvider.SqlServer:
            default:
                optionsBuilder.UseSqlServer(connectionString);
                break;
        }

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
