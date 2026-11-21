namespace LogMyDay.Api.Infrastructure.Data;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Says whose data the current scope works on. In a request it is the signed-in user. Code that
/// runs outside a request, or works on another user's data, opens a scope of its own through
/// <see cref="IUserScopeFactory"/>.
/// </summary>
public sealed class UserDatabaseScope
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private Guid? _explicitUserId;
    private bool _resolved;

    public UserDatabaseScope(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// The user this scope works for, or null. Once asked, the answer is fixed for the scope: the
    /// data context is already pointed at that user's database.
    /// </summary>
    public Guid? UserId
    {
        get
        {
            _resolved = true;

            return _explicitUserId ??= FromRequest();
        }
    }

    /// <summary>Makes the scope work for the given user. Call it before anything reaches the data.</summary>
    public void Use(Guid userId)
    {
        if (_explicitUserId == userId)
        {
            return;
        }

        if (_resolved || _explicitUserId != null)
        {
            throw new InvalidOperationException(
                "This scope already works for a user. Open a new scope for another user (IUserScopeFactory).");
        }

        _explicitUserId = userId;
    }

    private Guid? FromRequest()
    {
        var claim = _httpContextAccessor?.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var userId) ? userId : null;
    }
}

/// <summary>Opens a scope that works on one user's data, outside that user's own request.</summary>
public interface IUserScopeFactory
{
    /// <summary>A new scope whose services read and write the given user's database.</summary>
    AsyncServiceScope Create(Guid userId);
}

public sealed class UserScopeFactory : IUserScopeFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public UserScopeFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public AsyncServiceScope Create(Guid userId)
    {
        var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<UserDatabaseScope>().Use(userId);

        return scope;
    }
}
