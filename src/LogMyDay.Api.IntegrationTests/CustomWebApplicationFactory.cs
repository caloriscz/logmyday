using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogMyDay.Api.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestUserEmail = "test@example.com";
    public const string TestUserPassword = "Integration-Test-Pa55";

    /// <summary>
    /// The MCP tests act as their own user, so what they log never changes the counts other tests
    /// assert on the seeded user; the known tokens' hashes are seeded as that user's keys.
    /// </summary>
    public const string McpUserEmail = "mcp@example.com";
    public const string ReadWriteKeyToken = "lmd_IntegrationTestReadWriteKey0000000000000000";
    public const string ReadOnlyKeyToken = "lmd_IntegrationTestReadOnlyKey00000000000000000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        
        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related descriptors to avoid provider conflicts
            var descriptors = services.Where(
                d => d.ServiceType == typeof(DbContextOptions<LogMyDayDbContext>) ||
                     d.ServiceType == typeof(LogMyDayDbContext) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     (d.ServiceType.IsGenericType && 
                      d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)))
                .ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Add InMemoryDatabase for testing - use static name so all DbContext instances connect to same database
            services.AddDbContext<LogMyDayDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb");
            });

            // Build service provider and seed data
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            db.Database.EnsureCreated();
            SeedTestData(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        });
    }

    private static void SeedTestData(LogMyDayDbContext db, IPasswordHasher passwordHasher)
    {
        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = TestUserEmail,
            PasswordHash = passwordHasher.Hash(TestUserPassword)
        };
        db.Users.Add(testUser);

        var mcpUser = new User
        {
            Id = Guid.NewGuid(),
            Email = McpUserEmail,
            PasswordHash = "not-used"
        };
        db.Users.Add(mcpUser);
        db.ApiKeys.AddRange(
            SeededKey(mcpUser.Id, "rw", ReadWriteKeyToken, ApiKeyScope.ReadWrite),
            SeededKey(mcpUser.Id, "ro", ReadOnlyKeyToken, ApiKeyScope.ReadOnly));

        var tag1 = new Tag
        {
            TagName = "Exercise",
            UserId = testUser.Id,
            TimeGranularity = TimeGranularity.Daily,
            InputTypeId = 4 // Boolean
        };
        var tag2 = new Tag
        {
            TagName = "Sleep",
            UserId = testUser.Id,
            TimeGranularity = TimeGranularity.Exact,
            InputTypeId = 1 // Integer
        };
        var tag3 = new Tag
        {
            TagName = "Mood",
            UserId = testUser.Id,
            TimeGranularity = TimeGranularity.Hourly,
            InputTypeId = 2 // String
        };
        db.Tags.AddRange(tag1, tag2, tag3);
        db.SaveChanges(); // Save to get auto-generated Tag IDs

        db.Activities.AddRange(
            new Activity { TagId = tag1.Id, UserId = testUser.Id, DateStarted = new DateTime(2024, 6, 15, 10, 0, 0), Description = "Morning run" },
            new Activity { TagId = tag2.Id, UserId = testUser.Id, DateStarted = new DateTime(2024, 6, 15, 22, 30, 0), Description = "8" },
            new Activity { TagId = tag1.Id, UserId = testUser.Id, DateStarted = new DateTime(2025, 1, 10, 8, 0, 0), Description = "Gym session" },
            new Activity { TagId = tag3.Id, UserId = testUser.Id, DateStarted = new DateTime(2025, 1, 10, 14, 0, 0), Description = "Happy" },
            new Activity { TagId = tag2.Id, UserId = testUser.Id, DateStarted = new DateTime(2026, 1, 20, 23, 0, 0), Description = "7" }
        );

        db.SaveChanges();
    }

    private static ApiKey SeededKey(Guid userId, string name, string token, ApiKeyScope scope)
    {
        if (token.Length != ApiKeyService.TokenLength)
        {
            throw new InvalidOperationException($"Seeded token '{name}' must be {ApiKeyService.TokenLength} characters.");
        }

        return new ApiKey
        {
            UserId = userId,
            Name = name,
            Prefix = token[..ApiKeyService.PrefixLength],
            KeyHash = ApiKeyService.Hash(token),
            Scope = scope,
            CreatedUtc = DateTime.UtcNow
        };
    }
}
