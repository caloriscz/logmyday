using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace LogMyDay.Api.Application.Services;

public sealed class UserService : IUserService
{
    private readonly LogMyDayDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UserService> _logger;
    private readonly IEmailSender _emailSender;

    public UserService(LogMyDayDbContext context, IPasswordHasher passwordHasher, ILogger<UserService> logger, IEmailSender emailSender)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _emailSender = emailSender;
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<User> CreateFirstAdminAsync(string email, string password, string? displayName, CancellationToken cancellationToken)
    {
        var userCount = await _context.Users.CountAsync(cancellationToken);
        if (userCount > 0)
        {
            throw new InvalidOperationException("First admin can only be created when no users exist.");
        }

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var passwordHash = _passwordHasher.Hash(password);

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = displayName,
            PasswordHash = passwordHash,
            IsAdmin = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("First admin user created with email: {Email}", normalizedEmail);
        return user;
    }

    public async Task<User> CreateUserAsync(string email, string password, string? displayName, bool isAdmin, Guid actorId, CancellationToken cancellationToken)
    {
        var actor = await GetUserAndEnsureAdminAsync(actorId, cancellationToken);

        var normalizedEmail = email.ToLowerInvariant().Trim();
        
        var existingUser = await FindByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser != null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var passwordHash = _passwordHasher.Hash(password);

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = displayName,
            PasswordHash = passwordHash,
            IsAdmin = isAdmin
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User created with email: {Email} by admin: {ActorId}", normalizedEmail, actorId);
        return user;
    }

    public async Task<User?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Users.FindAsync([id], cancellationToken);
    }

    public async Task<List<User>> ListAsync(CancellationToken cancellationToken)
    {
        return await _context.Users
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);
    }

    public async Task<User> UpdateAsync(Guid id, string? email, string? displayName, bool? isAdmin, Guid actorId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(id, cancellationToken);
        var actor = await GetUserAsync(actorId, cancellationToken);

        // Admin can update any user, non-admin can only update themselves (limited fields)
        if (!actor.IsAdmin && actor.Id != user.Id)
        {
            throw new UnauthorizedAccessException("You can only update your own profile.");
        }

        // Non-admin users cannot change isAdmin flag
        if (!actor.IsAdmin && isAdmin.HasValue)
        {
            throw new UnauthorizedAccessException("You cannot change admin status.");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.ToLowerInvariant().Trim();
            var existingUser = await FindByEmailAsync(normalizedEmail, cancellationToken);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }
            user.Email = normalizedEmail;
        }

        if (displayName != null)
        {
            user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        }

        if (isAdmin.HasValue && actor.IsAdmin)
        {
            user.IsAdmin = isAdmin.Value;
        }

        user.UpdatedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} updated by {ActorId}", user.Id, actorId);
        return user;
    }

    public async Task DeleteAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var actor = await GetUserAndEnsureAdminAsync(actorId, cancellationToken);
        
        if (actor.Id == id)
        {
            throw new InvalidOperationException("You cannot delete yourself.");
        }

        var user = await GetUserAsync(id, cancellationToken);
        
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} deleted by admin {ActorId}", id, actorId);
    }

    public async Task ChangePasswordAsync(Guid id, string currentPassword, string newPassword, Guid actorId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(id, cancellationToken);
        var actor = await GetUserAsync(actorId, cancellationToken);

        // Admin can change any password, non-admin can only change their own
        if (!actor.IsAdmin && actor.Id != user.Id)
        {
            throw new UnauthorizedAccessException("You can only change your own password.");
        }

        // Non-admin users must provide current password
        if (!actor.IsAdmin && !_passwordHasher.Verify(currentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is incorrect.");
        }

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.UpdatedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for user {UserId} by {ActorId}", id, actorId);
    }

    public async Task AdminResetPasswordAsync(Guid id, string newPassword, Guid actorId, CancellationToken cancellationToken)
    {
        await GetUserAndEnsureAdminAsync(actorId, cancellationToken);
        var user = await GetUserAsync(id, cancellationToken);

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.UpdatedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset for user {UserId} by admin {ActorId}", id, actorId);
    }

    public async Task BeginForgotAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = await FindByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null)
        {
            // Don't reveal if email exists or not
            _ = GenerateSecureToken();
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", normalizedEmail);
            return;
        }

        // Generate a secure token
        var token = GenerateSecureToken();
        var expiry = DateTime.UtcNow.AddHours(1); // 1 hour expiry

        var passwordReset = new PasswordReset
        {
            UserId = user.Id,
            Token = token,
            ExpiresUtc = expiry
        };

        _context.PasswordResets.Add(passwordReset);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailSender.SendPasswordResetEmailAsync(user.Email, user.DisplayName, token, cancellationToken);
            _logger.LogInformation("Password reset token generated and email sent for user {UserId}", user.Id);
        }
        catch
        {
            _context.PasswordResets.Remove(passwordReset);
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task CompleteForgotAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        var passwordReset = await _context.PasswordResets
            .Include(pr => pr.User)
            .FirstOrDefaultAsync(pr => pr.Token == token && pr.UsedUtc == null && pr.ExpiresUtc > DateTime.UtcNow, cancellationToken);

        if (passwordReset?.User == null)
        {
            throw new InvalidOperationException("Invalid or expired reset token.");
        }

        // Update password
        passwordReset.User.PasswordHash = _passwordHasher.Hash(newPassword);
        passwordReset.User.UpdatedUtc = DateTime.UtcNow;

        // Mark token as used
        passwordReset.UsedUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset completed for user {UserId}", passwordReset.UserId);
    }

    private async Task<User> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await GetAsync(id, cancellationToken);
        if (user == null)
        {
            throw new ArgumentException($"User with ID {id} not found.", nameof(id));
        }
        return user;
    }

    private async Task<User> GetUserAndEnsureAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(id, cancellationToken);
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException("This operation requires admin privileges.");
        }
        return user;
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
