using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private readonly LogMyDayDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(LogMyDayDbContext context, IPasswordHasher passwordHasher, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Check if any users exist
            var userCount = await _context.Users.CountAsync();
            if (userCount > 0)
            {
                _logger.LogInformation("Users already exist in database. Skipping seeding.");
                return;
            }

            // Create the admin user with the same credentials as Basic Auth
            var adminUser = new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), // Same as Basic Auth UserId
                Email = "admin@logmyday.com", // Convert username to email format
                DisplayName = "Administrator",
                PasswordHash = _passwordHasher.Hash("secret123"), // Same password as Basic Auth
                IsAdmin = true,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded admin user: {Email} with ID: {UserId}", 
                adminUser.Email, adminUser.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding database");
            throw;
        }
    }
}
