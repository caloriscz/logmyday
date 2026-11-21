using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// Prepares the databases at startup: the platform database with its first admin, and then the
/// database of every user, with the one-time migrations of its data.
/// </summary>
public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private readonly PlatformDbContext _platform;
    private readonly UserDatabases _databases;
    private readonly IUserScopeFactory _userScopes;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(PlatformDbContext platform, UserDatabases databases, IUserScopeFactory userScopes,
        IPasswordHasher passwordHasher, ILogger<DatabaseSeeder> logger)
    {
        _platform = platform;
        _databases = databases;
        _userScopes = userScopes;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await CreatePlatform();
            await SeedAdmin();

            var userIds = await _platform.Users.Select(u => u.Id).ToListAsync();
            foreach (var userId in userIds)
            {
                await PrepareUserDatabase(userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding database");
            throw;
        }
    }

    /// <summary>
    /// The platform database has no migrations yet, so a new one is created from the model. A
    /// database that already has the platform tables is used as it is.
    /// </summary>
    private async Task CreatePlatform()
    {
        var creator = _platform.GetService<IRelationalDatabaseCreator>();
        if (await creator.ExistsAsync() && await creator.HasTablesAsync())
        {
            return;
        }

        await _platform.Database.EnsureCreatedAsync();
        _logger.LogInformation("Created the platform database");
    }

    private async Task SeedAdmin()
    {
        if (await _platform.Users.AnyAsync())
        {
            _logger.LogInformation("Users already exist in database. Skipping admin user seeding.");

            return;
        }

        // Create the admin user with the same credentials as Basic Auth
        var adminUser = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), // Same as Basic Auth UserId
            Email = "admin", // Convert username to email format
            DisplayName = "Administrator",
            PasswordHash = _passwordHasher.Hash("secret123"), // Same password as Basic Auth
            IsAdmin = true,
            Culture = PreferencesFactory.DefaultCulture,
            TimeZone = PreferencesFactory.DefaultTimeZoneId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _platform.Users.Add(adminUser);
        await _platform.SaveChangesAsync();

        _logger.LogInformation("Successfully seeded admin user: {Email} with ID: {UserId}",
            adminUser.Email, adminUser.Id);
    }

    /// <summary>The user's database with its schema and reference data, then its one-time data migrations.</summary>
    private async Task PrepareUserDatabase(Guid userId)
    {
        _databases.Ensure(userId);

        await using var scope = _userScopes.Create(userId);
        await scope.ServiceProvider.GetRequiredService<IReminderDayBackfill>().RunAsync();
        await scope.ServiceProvider.GetRequiredService<IReminderRecurrenceMigration>().RunAsync();
    }
}
