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
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        Console.WriteLine($"[LogMyDayDbContextFactory] Connection String found: {(string.IsNullOrEmpty(connectionString) ? "NO" : "YES")}");
        
        if (!string.IsNullOrEmpty(connectionString))
        {
             // Mask password for logging
             var masked = System.Text.RegularExpressions.Regex.Replace(connectionString, "Password=.*?;", "Password=***;");
             Console.WriteLine($"[LogMyDayDbContextFactory] Using Connection String: {masked}");
        }

        // Fallback if configuration is missing (e.g. running from a location where config isn't found)
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("[LogMyDayDbContextFactory] ERROR: Connection string 'DefaultConnection' is null or empty.");
            Console.WriteLine("[LogMyDayDbContextFactory] Please ensure appsettings.json or appsettings.Development.json exists in LogMyDay.App and contains the connection string.");
            throw new InvalidOperationException("Could not find connection string 'DefaultConnection'.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<LogMyDayDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

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
