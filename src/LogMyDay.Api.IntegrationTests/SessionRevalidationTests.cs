using System.Security.Claims;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.App.Authentication;
using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// An open page checks its account every minute as the cookie does on each request: it stays
/// signed in only while the account exists with the password and admin right it was signed in with.
/// </summary>
public class SessionRevalidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Revalidation-Pa55word!";

    private readonly CustomWebApplicationFactory _factory;

    public SessionRevalidationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private IServiceScopeFactory Scopes => _factory.Services.GetRequiredService<IServiceScopeFactory>();

    [Fact]
    public async Task AnUnchangedAccount_StaysSignedIn()
    {
        var (_, user) = await CreateUser();

        Assert.True(await SessionRevalidatingAuthenticationStateProvider.IsCurrent(PageSignedInAs(user), Scopes, CancellationToken.None));
    }

    [Fact]
    public async Task AChangedPassword_EndsTheOpenPage()
    {
        var (_, user) = await CreateUser();
        var page = PageSignedInAs(user);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserService>()
                .ChangePassword(user.Id, Password, "Changed-Pa55word!", user.Id, CancellationToken.None);
        }

        Assert.False(await SessionRevalidatingAuthenticationStateProvider.IsCurrent(page, Scopes, CancellationToken.None));
    }

    [Fact]
    public async Task AChangedAdminRight_EndsTheOpenPage()
    {
        var (adminId, user) = await CreateUser();
        var page = PageSignedInAs(user);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserService>()
                .Update(user.Id, null, null, isAdmin: true, null, null, adminId, CancellationToken.None);
        }

        Assert.False(await SessionRevalidatingAuthenticationStateProvider.IsCurrent(page, Scopes, CancellationToken.None));
    }

    [Fact]
    public async Task ADeletedAccount_EndsTheOpenPage()
    {
        var (adminId, user) = await CreateUser();
        var page = PageSignedInAs(user);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserService>().Delete(user.Id, adminId, CancellationToken.None);
        }

        Assert.False(await SessionRevalidatingAuthenticationStateProvider.IsCurrent(page, Scopes, CancellationToken.None));
    }

    [Fact]
    public async Task APageSignedInBeforeTheStampExisted_IsSignedOut()
    {
        var (_, user) = await CreateUser();
        var page = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "lmd-cookie"));

        Assert.False(await SessionRevalidatingAuthenticationStateProvider.IsCurrent(page, Scopes, CancellationToken.None));
    }

    /// <summary>The claims a sign-in gives the cookie, as the page holds them.</summary>
    private static ClaimsPrincipal PageSignedInAs(User user)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("is_admin", user.IsAdmin.ToString().ToLowerInvariant()),
                new Claim(SessionStamp.ClaimType, SessionStamp.For(user)),
            ],
            "lmd-cookie"));
    }

    private async Task<(Guid AdminId, User User)> CreateUser()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        var adminId = (await TestPlatform.For(db).Users.FirstAsync(u => u.Email == CustomWebApplicationFactory.AdminUserEmail)).Id;
        var user = await scope.ServiceProvider.GetRequiredService<IUserService>()
            .CreateUser($"page-{Guid.NewGuid():N}@example.com", Password, "Page", false, "en-US", "UTC", adminId, CancellationToken.None);

        return (adminId, user);
    }
}
