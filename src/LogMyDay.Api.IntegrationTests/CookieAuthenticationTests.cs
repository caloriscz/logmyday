using System.Net;
using System.Net.Http.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>The sign-in cookie of the web app, end to end.</summary>
public class CookieAuthenticationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Cookie-Test-Pa55word!";

    private readonly CustomWebApplicationFactory _factory;

    public CookieAuthenticationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ADeletedUsersCookie_IsSignedOut_OnTheNextRequest()
    {
        var email = $"cookie-{Guid.NewGuid():N}@example.com";
        Guid adminId;
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            adminId = (await TestPlatform.For(db).Users.FirstAsync(u => u.Email == CustomWebApplicationFactory.AdminUserEmail)).Id;
            var users = scope.ServiceProvider.GetRequiredService<IUserService>();
            userId = (await users.CreateUser(email, Password, "Cookie", false, "en-US", "UTC", adminId, CancellationToken.None)).Id;
        }

        // The cookie is Secure, so the client only sends it over https.
        var browser = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/api/auth/login", new LoginDto(email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/tags")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserService>().Delete(userId, adminId, CancellationToken.None);
        }

        var me = await browser.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Contains(me.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("lmd.auth=;", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/tags")).StatusCode);
    }

    [Fact]
    public async Task ADemotedAdminsCookie_IsSignedOut_AndNoLongerPassesAdminOnly()
    {
        var (adminId, userId, email) = await CreateUser(isAdmin: true);
        var browser = await SignIn(email, Password);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/users")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserService>()
                .Update(userId, null, null, isAdmin: false, null, null, adminId, CancellationToken.None);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);

        // Signed in again, the user has the account's current rights.
        var again = await SignIn(email, Password);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await again.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task AChangedPassword_EndsTheSessions_ThatWereSignedInWithTheOldOne()
    {
        var (_, userId, email) = await CreateUser(isAdmin: false);
        var browser = await SignIn(email, Password);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserService>()
                .ChangePassword(userId, Password, "Changed-Pa55word!", userId, CancellationToken.None);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await SignIn(email, "Changed-Pa55word!")).GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AnAdminsPasswordReset_EndsTheUsersSessions()
    {
        var (adminId, userId, email) = await CreateUser(isAdmin: false);
        var browser = await SignIn(email, Password);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserService>()
                .AdminResetPassword(userId, "Reset-Pa55word!", adminId, CancellationToken.None);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AnExistingUsersCookie_KeepsWorking()
    {
        var browser = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var login = new LoginDto(CustomWebApplicationFactory.TestUserEmail, CustomWebApplicationFactory.TestUserPassword);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/api/auth/login", login)).StatusCode);

        for (var request = 0; request < 3; request++)
        {
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/auth/me")).StatusCode);
        }
    }

    private async Task<(Guid AdminId, Guid UserId, string Email)> CreateUser(bool isAdmin)
    {
        var email = $"cookie-{Guid.NewGuid():N}@example.com";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        var adminId = (await TestPlatform.For(db).Users.FirstAsync(u => u.Email == CustomWebApplicationFactory.AdminUserEmail)).Id;
        var user = await scope.ServiceProvider.GetRequiredService<IUserService>()
            .CreateUser(email, Password, "Cookie", isAdmin, "en-US", "UTC", adminId, CancellationToken.None);

        return (adminId, user.Id, email);
    }

    /// <summary>A client with its own cookie jar, signed in. The cookie is Secure, so it talks https.</summary>
    private async Task<HttpClient> SignIn(string email, string password)
    {
        var browser = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/api/auth/login", new LoginDto(email, password))).StatusCode);

        return browser;
    }
}
