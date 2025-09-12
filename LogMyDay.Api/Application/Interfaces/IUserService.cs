using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Application.Interfaces;

public interface IUserService
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User> CreateFirstAdminAsync(string email, string password, string? displayName, CancellationToken cancellationToken);
    Task<User> CreateUserAsync(string email, string password, string? displayName, bool isAdmin, Guid actorId, CancellationToken cancellationToken);
    Task<User?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<List<User>> ListAsync(CancellationToken cancellationToken);
    Task<User> UpdateAsync(Guid id, string? email, string? displayName, bool? isAdmin, Guid actorId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid actorId, CancellationToken cancellationToken);
    Task ChangePasswordAsync(Guid id, string currentPassword, string newPassword, Guid actorId, CancellationToken cancellationToken);
    Task AdminResetPasswordAsync(Guid id, string newPassword, Guid actorId, CancellationToken cancellationToken);
    Task<string> BeginForgotAsync(string email, CancellationToken cancellationToken);
    Task CompleteForgotAsync(string token, string newPassword, CancellationToken cancellationToken);
}
