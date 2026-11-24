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
public sealed class CookieAuthenticationTests : IDisposable
{
    private const string Password = "Cookie-Test-Pa55word!";

    // A host per test: together the tests sign in more often than the rate limit allows one client.
    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
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
    public async Task AChangedAdminRight_AppliesFromTheNextRequest_AndSignsNobodyOut()
    {
        var (adminId, userId, email) = await CreateUser(isAdmin: true);
        var browser = await SignIn(email, Password);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/users")).StatusCode);

        await SetAdmin(userId, isAdmin: false, adminId);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/auth/me")).StatusCode);

        await SetAdmin(userId, isAdmin: true, adminId);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task ChangingYourOwnPassword_KeepsThisBrowserSignedIn_AndSignsOutTheOthers()
    {
        var (_, _, email) = await CreateUser(isAdmin: false);
        var here = await SignIn(email, Password);
        var elsewhere = await SignIn(email, Password);

        var response = await PostPasswordForm(here, Password, "Changed-Pa55word!");

        Assert.Equal("/profile?password=changed", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await SignIn(email, "Changed-Pa55word!")).GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("not-the-password", "Changed-Pa55word!", "incorrect")]
    [InlineData("", "Changed-Pa55word!", "incorrect")]
    [InlineData(Password, "too-short", "short")]
    public async Task ARefusedPasswordChange_ChangesNothing_AndSignsNobodyOut(string current, string changed, string result)
    {
        var (_, _, email) = await CreateUser(isAdmin: false);
        var here = await SignIn(email, Password);
        var elsewhere = await SignIn(email, Password);

        var response = await PostPasswordForm(here, current, changed);

        Assert.Equal($"/profile?password={result}", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await elsewhere.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await SignIn(email, Password)).GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ALogout_DeletesTheBrowsersCookie()
    {
        var (_, _, email) = await CreateUser(isAdmin: false);
        var browser = await SignIn(email, Password);

        var response = await browser.PostAsync("/api/auth/logout-form", content: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("lmd.auth=;", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ALogoutFormWithoutTheCookie_DeletesNothing()
    {
        // As a form on another site: SameSite=Lax keeps the cookie from it.
        var response = await Browser().PostAsync("/api/auth/logout-form", content: null);

        Assert.Equal("/login", response.Headers.Location?.OriginalString);
        Assert.False(response.Headers.Contains("Set-Cookie"));
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

    private async Task SetAdmin(Guid userId, bool isAdmin, Guid actorId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUserService>()
            .Update(userId, null, null, isAdmin, null, null, actorId, CancellationToken.None);
    }

    private static Task<HttpResponseMessage> PostPasswordForm(HttpClient browser, string current, string changed)
    {
        return browser.PostAsync("/api/account/password/change-form", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["currentPassword"] = current,
            ["newPassword"] = changed,
        }));
    }

    /// <summary>A client with its own cookie jar that doesn't follow redirects. The cookie is Secure, so it talks https.</summary>
    private HttpClient Browser()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    private async Task<HttpClient> SignIn(string email, string password)
    {
        var browser = Browser();
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/api/auth/login", new LoginDto(email, password))).StatusCode);

        return browser;
    }
}
