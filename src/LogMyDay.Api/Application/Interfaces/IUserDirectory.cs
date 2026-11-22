using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Application.Interfaces;

/// <summary>
/// Read-only view of the installation's users for the services that work on a user's data.
/// Users live in the platform database and the data in the user's own database, so those
/// services can't read the user through their data context.
/// </summary>
public interface IUserDirectory
{
    /// <summary>The user, or null. Not tracked: changes to it are not saved.</summary>
    Task<User?> Find(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's time zone id, or null when unset or the user is unknown.</summary>
    Task<string?> GetTimeZone(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>All users. Not tracked.</summary>
    Task<IReadOnlyList<User>> List(CancellationToken cancellationToken = default);
}
