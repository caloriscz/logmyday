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
}
