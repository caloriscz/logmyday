using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using System.Security.Claims;

namespace LogMyDay.App.Authentication;

/// <summary>
/// An open page keeps the authentication state it was opened with. Every minute this checks the
/// account again as the sign-in cookie does on each request: when it was deleted, or its password
/// or admin right changed (<see cref="SessionStamp"/>), the page loses its authentication state,
/// and the layout sends the browser to the sign-in page.
/// </summary>
internal sealed class SessionRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopes;

    public SessionRevalidatingAuthenticationStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopes)
        : base(loggerFactory)
    {
        _scopes = scopes;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        return IsCurrent(authenticationState.User, _scopes, cancellationToken);
    }

    /// <summary>Whether the principal's account still exists, with the password and admin right it was signed in with.</summary>
    internal static async Task<bool> IsCurrent(ClaimsPrincipal principal, IServiceScopeFactory scopes, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return false;
        }

        await using var scope = scopes.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserDirectory>().Find(userId, cancellationToken);

        return user != null && SessionStamp.Matches(principal, user);
    }
}
