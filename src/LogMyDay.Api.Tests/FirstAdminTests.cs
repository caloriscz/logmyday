using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Options;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// The first start of a new installation creates the first admin with the password from the
/// configuration, so that no installation starts with a known password.
/// </summary>
public sealed class FirstAdminTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"lmd-first-admin-{Guid.NewGuid():N}");

    public FirstAdminTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task ANewInstallation_CreatesTheAdmin_WithTheConfiguredPassword()
    {
        await Start("first-admin-password");

        var admin = Assert.Single(await Users());
        Assert.Equal("admin", admin.Email);
        Assert.True(admin.IsAdmin);
        Assert.Equal("hash of first-admin-password", admin.PasswordHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ANewInstallation_WithoutThePassword_DoesNotStart_AndSaysWhereToSetIt(string? password)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(password));

        Assert.Contains("Admin:InitialPassword is not set", error.Message);
        Assert.Contains("Admin__InitialPassword", error.Message);
        Assert.Contains("admin_password", error.Message);
        Assert.Empty(await Users());
    }

    [Fact]
    public async Task ANewInstallation_WithAShortPassword_DoesNotStart()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Start("123456789"));

        Assert.Contains("shorter than 10 characters", error.Message);
        Assert.Empty(await Users());
    }

    [Fact]
    public async Task AnInstallationWithAccounts_IgnoresTheSetting_AndKeepsTheAdminsPassword()
    {
        await Start("first-admin-password");

        await Start("another-password");
        await Start(null);

        var admin = Assert.Single(await Users());
        Assert.Equal("hash of first-admin-password", admin.PasswordHash);
    }

    private async Task Start(string? initialPassword)
    {
        await using var services = Services(initialPassword);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>().SeedAsync();
    }

    private async Task<List<User>> Users()
    {
        await using var services = Services(initialPassword: null);
        await using var scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.ToListAsync();
    }

    private ServiceProvider Services(string? initialPassword)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(password => $"hash of {password}");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogMyDayDatabases($"Data Source={Path.Combine(_folder, "logmyday.db")}");
        services.AddSingleton(hasher.Object);
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IReminderDayBackfill, ReminderDayBackfill>();
        services.AddScoped<IReminderRecurrenceMigration, ReminderRecurrenceMigration>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        services.Configure<InitialAdminOptions>(o => o.InitialPassword = initialPassword);

        return services.BuildServiceProvider();
    }
}
