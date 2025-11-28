using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Application.Interfaces;

public interface IUserService
{
    Task<User?> FindByEmail(string email, CancellationToken cancellationToken);
    Task<User> CreateFirstAdmin(string email, string password, string? displayName, CancellationToken cancellationToken);
    Task<User> CreateUser(string email, string password, string? displayName, bool isAdmin, string culture, string timeZone, Guid actorId, CancellationToken cancellationToken);
    Task<User?> Get(Guid id, CancellationToken cancellationToken);
    Task<List<User>> List(CancellationToken cancellationToken);
    Task<User> Update(Guid id, string? email, string? displayName, bool? isAdmin, string? culture, string? timeZone, Guid actorId, CancellationToken cancellationToken);
    Task Delete(Guid id, Guid actorId, CancellationToken cancellationToken);
    Task ChangePassword(Guid id, string currentPassword, string newPassword, Guid actorId, CancellationToken cancellationToken);
    Task AdminResetPassword(Guid id, string newPassword, Guid actorId, CancellationToken cancellationToken);
    Task BeginForgot(string email, CancellationToken cancellationToken);
    Task CompleteForgot(string token, string newPassword, CancellationToken cancellationToken);
}
