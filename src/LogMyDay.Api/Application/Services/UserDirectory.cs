using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

public sealed class UserDirectory : IUserDirectory
{
    private readonly PlatformDbContext _platform;

    public UserDirectory(PlatformDbContext platform)
    {
        _platform = platform;
    }

    public Task<User?> Find(Guid userId, CancellationToken cancellationToken = default)
    {
        return _platform.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public Task<string?> GetTimeZone(Guid userId, CancellationToken cancellationToken = default)
    {
        return _platform.Users
            .Where(u => u.Id == userId)
            .Select(u => u.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> List(CancellationToken cancellationToken = default)
    {
        return await _platform.Users.AsNoTracking().ToListAsync(cancellationToken);
    }
}
