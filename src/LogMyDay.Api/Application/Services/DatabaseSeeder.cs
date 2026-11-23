using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Options;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// Prepares the databases at startup: splits a single-file database of v0.7.2 into the platform
/// database and the users' databases, migrates the platform database and seeds its first admin,
/// then migrates the database of every user and runs the one-time migrations of its data. Any
/// failure stops the start.
/// </summary>
public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private readonly PlatformDbContext _platform;
    private readonly UserDatabases _databases;
    private readonly SingleFileSplit _split;
    private readonly IUserScopeFactory _userScopes;
    private readonly IPasswordHasher _passwordHasher;
    private readonly InitialAdminOptions _initialAdmin;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(PlatformDbContext platform, UserDatabases databases, SingleFileSplit split, IUserScopeFactory userScopes,
        IPasswordHasher passwordHasher, IOptions<InitialAdminOptions> initialAdmin, ILogger<DatabaseSeeder> logger)
    {
        _platform = platform;
        _databases = databases;
        _split = split;
        _userScopes = userScopes;
        _passwordHasher = passwordHasher;
        _initialAdmin = initialAdmin.Value;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // An installation of v0.7.2 kept everything in the one file.
            _split.SplitIfNeeded();

            DatabaseUpgrade.Migrate(_platform, _databases.SnapshotFolder, _logger);
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
    /// The first admin of a new installation, with the password from the configuration: without
    /// one, the start stops rather than create an account with a known password.
    /// </summary>
    private async Task SeedAdmin()
    {
        var password = _initialAdmin.InitialPassword;
        if (await _platform.Users.AnyAsync())
        {
            _logger.LogInformation("Users already exist in database. Skipping admin user seeding.");
            if (!string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Admin:InitialPassword is ignored: the installation already has accounts. Remove it from the configuration");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(InitialPasswordHelp("Admin:InitialPassword is not set."));
        }

        if (password.Length < InitialAdminOptions.MinimumPasswordLength)
        {
            throw new InvalidOperationException(InitialPasswordHelp(
                $"Admin:InitialPassword is shorter than {InitialAdminOptions.MinimumPasswordLength} characters."));
        }

        var adminUser = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Email = InitialAdminOptions.Email,
            DisplayName = "Administrator",
            PasswordHash = _passwordHasher.Hash(password),
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

    private static string InitialPasswordHelp(string problem)
    {
        return $"This installation has no account yet, so this start creates the first admin, \"{InitialAdminOptions.Email}\", and needs its password. {problem} "
            + $"Set Admin:InitialPassword to at least {InitialAdminOptions.MinimumPasswordLength} characters: in appsettings.json, "
            + "as the environment variable Admin__InitialPassword, or in Docker in the admin_password secret. "
            + "Once the account exists, the setting is ignored and can be removed.";
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
