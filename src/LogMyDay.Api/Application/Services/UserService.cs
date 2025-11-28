using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
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

    public async Task<User?> FindByEmail(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        return await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<User> CreateFirstAdmin(string email, string password, string? displayName, CancellationToken cancellationToken)
    {
        var userCount = await _context.Users.CountAsync(cancellationToken);
        if (userCount > 0)
        {
            throw new InvalidOperationException("First admin can only be created when no users exist.");
        }

        var normalizedEmail = email.ToLowerInvariant().Trim();
        var passwordHash = _passwordHasher.Hash(password);

        var defaultCulture = NormalizeCultureOrThrow(null);
        var defaultTimeZone = NormalizeTimeZoneOrThrow(null);

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            PasswordHash = passwordHash,
            IsAdmin = true,
            Culture = defaultCulture,
            TimeZone = defaultTimeZone
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("First admin user created with email: {Email}", normalizedEmail);

        return user;
    }

    public async Task<User> CreateUser(string email, string password, string? displayName, bool isAdmin, string culture, string timeZone, Guid actorId, CancellationToken cancellationToken)
    {
        _ = await GetUserAndEnsureAdminAsync(actorId, cancellationToken);

        var normalizedEmail = email.ToLowerInvariant().Trim();

        if (string.IsNullOrWhiteSpace(culture))
        {
            throw new InvalidOperationException("Culture is required.");
        }

        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new InvalidOperationException("Time zone is required.");
        }

        var normalizedCulture = NormalizeCultureOrThrow(culture);
        var normalizedTimeZone = NormalizeTimeZoneOrThrow(timeZone);

        var existingUser = await FindByEmail(normalizedEmail, cancellationToken);
        if (existingUser != null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var passwordHash = _passwordHasher.Hash(password);

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            PasswordHash = passwordHash,
            IsAdmin = isAdmin,
            Culture = normalizedCulture,
            TimeZone = normalizedTimeZone
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User created with email: {Email} by admin: {ActorId}", normalizedEmail, actorId);

        return user;
    }

    public async Task<User?> Get(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Users.FindAsync([id], cancellationToken);
    }

    public async Task<List<User>> List(CancellationToken cancellationToken)
    {
        return await _context.Users.OrderBy(u => u.Email).ToListAsync(cancellationToken);
    }

    public async Task<User> Update(Guid id, string? email, string? displayName, bool? isAdmin, string? culture, string? timeZone, Guid actorId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔧 UserService.Update: Starting update for user {UserId} by actor {ActorId}", id, actorId);
        
        var user = await GetUserAsync(id, cancellationToken);
        _logger.LogInformation("🔧 UserService.Update: Found user - Id={UserId}, Email={Email}, IsAdmin={IsAdmin}", user.Id, user.Email, user.IsAdmin);
        
        var actor = await GetUserAsync(actorId, cancellationToken);
        _logger.LogInformation("🔧 UserService.Update: Found actor - Id={ActorId}, Email={Email}, IsAdmin={IsAdmin}", actor.Id, actor.Email, actor.IsAdmin);
        
        _logger.LogInformation("🔧 UserService.Update: Checking authorization - actor.IsAdmin={IsAdmin}, actor.Id={ActorId}, user.Id={UserId}, IsSameUser={IsSame}", 
            actor.IsAdmin, actor.Id, user.Id, actor.Id == user.Id);

        // Admin can update any user, non-admin can only update themselves (limited fields)
        if (!actor.IsAdmin && actor.Id != user.Id)
        {
            _logger.LogWarning("🔧 UserService.Update: Authorization failed - Non-admin trying to update different user");
            throw new UnauthorizedAccessException("You can only update your own profile.");
        }

        _logger.LogInformation("🔧 UserService.Update: Authorization passed - proceeding with update");

        // Non-admin users cannot change isAdmin flag
        if (!actor.IsAdmin && isAdmin.HasValue)
        {
            _logger.LogWarning("🔧 UserService.Update: Non-admin trying to change admin status - isAdmin={IsAdmin}", isAdmin);
            throw new UnauthorizedAccessException("You cannot change admin status.");
        }

        _logger.LogInformation("🔧 UserService.Update: Updating fields - email={HasEmail}, displayName={HasDisplayName}, isAdmin={IsAdmin}, culture={Culture}, timeZone={TimeZone}", 
            !string.IsNullOrWhiteSpace(email), displayName != null, isAdmin, culture, timeZone);

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.ToLowerInvariant().Trim();
            var existingUser = await FindByEmail(normalizedEmail, cancellationToken);
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

        if (culture != null)
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                throw new InvalidOperationException("Culture is required.");
            }

            user.Culture = NormalizeCultureOrThrow(culture);
        }

        if (timeZone != null)
        {
            if (string.IsNullOrWhiteSpace(timeZone))
            {
                throw new InvalidOperationException("Time zone is required.");
            }

            user.TimeZone = NormalizeTimeZoneOrThrow(timeZone);
        }

        user.UpdatedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} updated by {ActorId}", user.Id, actorId);
        return user;
    }

    public async Task Delete(Guid id, Guid actorId, CancellationToken cancellationToken)
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

    public async Task ChangePassword(Guid id, string currentPassword, string newPassword, Guid actorId, CancellationToken cancellationToken)
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

    public async Task AdminResetPassword(Guid id, string newPassword, Guid actorId, CancellationToken cancellationToken)
    {
        await GetUserAndEnsureAdminAsync(actorId, cancellationToken);
        var user = await GetUserAsync(id, cancellationToken);

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.UpdatedUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset for user {UserId} by admin {ActorId}", id, actorId);
    }

    public async Task BeginForgot(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var user = await FindByEmail(normalizedEmail, cancellationToken);
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

    public async Task CompleteForgot(string token, string newPassword, CancellationToken cancellationToken)
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
        var user = await Get(id, cancellationToken);
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

    private static string NormalizeCultureOrThrow(string? culture)
    {
        try
        {
            return PreferencesFactory.NormalizeCulture(culture);
        }
        catch (CultureNotFoundException ex)
        {
            throw new InvalidOperationException($"Culture '{FormatPreferenceValue(culture)}' is not supported.", ex);
        }
    }

    private static string NormalizeTimeZoneOrThrow(string? timeZone)
    {
        try
        {
            return PreferencesFactory.NormalizeTimeZone(timeZone);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new InvalidOperationException($"Time zone '{FormatPreferenceValue(timeZone)}' is not supported.", ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new InvalidOperationException($"Time zone '{FormatPreferenceValue(timeZone)}' is not valid.", ex);
        }
    }

    private static string FormatPreferenceValue(string? value) => string.IsNullOrWhiteSpace(value) ? "<empty>" : value;

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
