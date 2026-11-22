using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// An admin's operations on other users' data. A request reaches only its own user's database,
/// so each operation runs in a scope opened for the user it works on.
/// </summary>
public interface IUserDataAdminService
{
    /// <summary>A JSON backup of one user's data.</summary>
    Task<BackupData> ExportData(Guid userId, CancellationToken cancellationToken);

    /// <summary>Clears one user's data, or every user's when no user is given. Accounts stay.</summary>
    /// <returns>The number of records cleared.</returns>
    Task<int> ClearData(Guid? userId, CancellationToken cancellationToken);
}

public sealed class UserDataAdminService : IUserDataAdminService
{
    private readonly IUserService _users;
    private readonly IUserScopeFactory _userScopes;

    public UserDataAdminService(IUserService users, IUserScopeFactory userScopes)
    {
        _users = users;
        _userScopes = userScopes;
    }

    public async Task<BackupData> ExportData(Guid userId, CancellationToken cancellationToken)
    {
        await Require(userId, cancellationToken);

        await using var scope = _userScopes.Create(userId);

        return await scope.ServiceProvider.GetRequiredService<IBackupService>().ExportDataAsync(userId);
    }

    public async Task<int> ClearData(Guid? userId, CancellationToken cancellationToken)
    {
        List<Guid> userIds;
        if (userId is { } one)
        {
            await Require(one, cancellationToken);
            userIds = [one];
        }
        else
        {
            userIds = (await _users.List(cancellationToken)).Select(u => u.Id).ToList();
        }

        var cleared = 0;
        foreach (var id in userIds)
        {
            await using var scope = _userScopes.Create(id);
            cleared += await scope.ServiceProvider.GetRequiredService<IBackupService>().ClearDataAsync(id);
        }

        return cleared;
    }

    private async Task Require(Guid userId, CancellationToken cancellationToken)
    {
        _ = await _users.Get(userId, cancellationToken) ?? throw new KeyNotFoundException("User not found");
    }
}
