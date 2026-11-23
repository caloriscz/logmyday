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
/// The first start of a new installation creates the first admin with the email and password
/// from the configuration, so that no installation starts with a known account.
/// </summary>
public sealed class FirstAdminTests : IDisposable
{
    private const string AdminEmail = "owner@example.com";

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
    public async Task ANewInstallation_CreatesTheAdmin_WithTheConfiguredEmailAndPassword()
    {
        await Start(AdminEmail, "first-admin-password");

        var admin = Assert.Single(await Users());
        Assert.Equal(AdminEmail, admin.Email);
        Assert.True(admin.IsAdmin);
        Assert.Equal("hash of first-admin-password", admin.PasswordHash);
    }

    [Fact]
    public async Task TheAdminsEmail_IsStoredTheWayTheSignInLooksItUp()
    {
        await Start("  Owner@Example.COM ", "first-admin-password");

        Assert.Equal(AdminEmail, Assert.Single(await Users()).Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ANewInstallation_WithoutThePassword_DoesNotStart_AndSaysWhereToSetIt(string? password)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(AdminEmail, password));

        Assert.Contains("Admin:InitialPassword is not set", error.Message);
        Assert.Contains("Admin__InitialPassword", error.Message);
        Assert.Contains("admin_password", error.Message);
        Assert.Empty(await Users());
    }

    [Fact]
    public async Task ANewInstallation_WithAShortPassword_DoesNotStart()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(AdminEmail, "123456789"));

        Assert.Contains("shorter than 10 characters", error.Message);
        Assert.Empty(await Users());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task ANewInstallation_WithoutTheEmail_DoesNotStart_AndSaysWhereToSetIt(string? email)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(email, "first-admin-password"));

        Assert.Contains("Admin:Email is not set", error.Message);
        Assert.Contains("Admin__Email", error.Message);
        Assert.Contains("ADMIN_EMAIL", error.Message);
        Assert.Empty(await Users());
    }

    [Fact]
    public async Task AnAdminNamedAdmin_IsRefused_BecauseTheSignInPageAsksForAnEmail()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Start("admin", "first-admin-password"));

        Assert.Contains("Admin:Email \"admin\" is not an email address", error.Message);
        Assert.Empty(await Users());
    }

    [Fact]
    public async Task ANewInstallation_WithNeitherSetting_NamesBoth()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(null, null));

        Assert.Contains("Admin:Email is not set", error.Message);
        Assert.Contains("Admin:InitialPassword is not set", error.Message);
    }

    [Fact]
    public async Task AnInstallationWithAccounts_IgnoresTheSettings_AndKeepsTheAdmin()
    {
        await Start(AdminEmail, "first-admin-password");

        await Start("someone-else@example.com", "another-password");
        await Start(null, null);

        var admin = Assert.Single(await Users());
        Assert.Equal(AdminEmail, admin.Email);
        Assert.Equal("hash of first-admin-password", admin.PasswordHash);
    }

    private async Task Start(string? email, string? initialPassword)
    {
        await using var services = Services(email, initialPassword);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>().SeedAsync();
    }

    private async Task<List<User>> Users()
    {
        await using var services = Services(email: null, initialPassword: null);
        await using var scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.ToListAsync();
    }

    private ServiceProvider Services(string? email, string? initialPassword)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(password => $"hash of {password}");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogMyDayDatabases($"Data Source={Path.Combine(_folder, "logmyday.db")}");
        services.AddSingleton(hasher.Object);
        services.AddSingleton(new Mock<IEmailSender>().Object);
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IReminderDayBackfill, ReminderDayBackfill>();
        services.AddScoped<IReminderRecurrenceMigration, ReminderRecurrenceMigration>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        services.Configure<InitialAdminOptions>(o =>
        {
            o.Email = email;
            o.InitialPassword = initialPassword;
        });

        return services.BuildServiceProvider();
    }
}
