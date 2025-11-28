using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Tests for UserService focusing on authentication security:
/// - Password hashing validation
/// - Registration logic
/// - Login credential validation
/// - Duplicate user prevention
/// </summary>
public class UserServiceTests
{
    private static LogMyDayDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .Options;
        return new LogMyDayDbContext(options);
    }

    [Fact]
    public async Task CreateFirstAdmin_WithValidData_CreatesUserWithHashedPassword()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        var email = "admin@test.com";
        var password = "SecurePassword123!";
        var displayName = "Test Admin";

        // Act
        var user = await service.CreateFirstAdmin(email, password, displayName, CancellationToken.None);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(email.ToLowerInvariant(), user.Email);
        Assert.Equal(displayName, user.DisplayName);
        Assert.True(user.IsAdmin);
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual(password, user.PasswordHash); // Password should be hashed, not stored as plaintext
        
        // Verify password can be verified with hasher
        Assert.True(passwordHasher.Verify(password, user.PasswordHash));
    }

    [Fact]
    public async Task CreateFirstAdmin_WhenUsersExist_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        // Create first admin
        await service.CreateFirstAdmin("first@test.com", "Password123!", "First Admin", CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CreateFirstAdmin("second@test.com", "Password123!", "Second Admin", CancellationToken.None)
        );
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        // Create admin user
        var admin = await service.CreateFirstAdmin("admin@test.com", "AdminPass123!", "Admin", CancellationToken.None);

        // Create first regular user
        await service.CreateUser("user@test.com", "UserPass123!", "User One", false, "en-US", "UTC", admin.Id, CancellationToken.None);

        // Act & Assert - Try to create user with same email
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CreateUser("user@test.com", "DifferentPass!", "User Two", false, "en-US", "UTC", admin.Id, CancellationToken.None)
        );
    }

    [Fact]
    public async Task CreateUser_NormalizesEmailToLowerCase()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        var admin = await service.CreateFirstAdmin("admin@test.com", "AdminPass123!", "Admin", CancellationToken.None);

        // Act
        var user = await service.CreateUser("User@TEST.COM", "Password123!", "Test User", false, "en-US", "UTC", admin.Id, CancellationToken.None);

        // Assert
        Assert.Equal("user@test.com", user.Email); // Should be normalized to lowercase
    }

    [Fact]
    public async Task FindByEmail_IsCaseInsensitive()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        var createdUser = await service.CreateFirstAdmin("test@example.com", "Password123!", "Test", CancellationToken.None);

        // Act - Search with different casing
        var foundUser1 = await service.FindByEmail("TEST@EXAMPLE.COM", CancellationToken.None);
        var foundUser2 = await service.FindByEmail("Test@Example.Com", CancellationToken.None);
        var foundUser3 = await service.FindByEmail("test@example.com", CancellationToken.None);

        // Assert - All should find the same user
        Assert.NotNull(foundUser1);
        Assert.NotNull(foundUser2);
        Assert.NotNull(foundUser3);
        Assert.Equal(createdUser.Id, foundUser1!.Id);
        Assert.Equal(createdUser.Id, foundUser2!.Id);
        Assert.Equal(createdUser.Id, foundUser3!.Id);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_UpdatesPasswordHash()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        var user = await service.CreateFirstAdmin("user@test.com", "OldPassword123!", "User", CancellationToken.None);
        var oldPasswordHash = user.PasswordHash;

        // Act
        await service.ChangePassword(user.Id, "OldPassword123!", "NewPassword456!", user.Id, CancellationToken.None);

        // Assert
        var updatedUser = await service.Get(user.Id, CancellationToken.None);
        Assert.NotNull(updatedUser);
        Assert.NotEqual(oldPasswordHash, updatedUser!.PasswordHash); // Hash should change
        Assert.True(passwordHasher.Verify("NewPassword456!", updatedUser.PasswordHash)); // New password works
        Assert.False(passwordHasher.Verify("OldPassword123!", updatedUser.PasswordHash)); // Old password doesn't work
    }

    [Fact]
    public async Task ChangePassword_WithIncorrectCurrentPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        var admin = await service.CreateFirstAdmin("admin@test.com", "AdminPass123!", "Admin", CancellationToken.None);
        var user = await service.CreateUser("user@test.com", "CorrectPassword123!", "User", false, "en-US", "UTC", admin.Id, CancellationToken.None);

        // Act & Assert - Non-admin user trying to change their own password with wrong current password
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await service.ChangePassword(user.Id, "WrongPassword!", "NewPassword456!", user.Id, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ChangePassword_NonAdminChangingOtherUserPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        var admin = await service.CreateFirstAdmin("admin@test.com", "AdminPass123!", "Admin", CancellationToken.None);
        var user1 = await service.CreateUser("user1@test.com", "Pass123!", "User 1", false, "en-US", "UTC", admin.Id, CancellationToken.None);
        var user2 = await service.CreateUser("user2@test.com", "Pass123!", "User 2", false, "en-US", "UTC", admin.Id, CancellationToken.None);

        // Act & Assert - User 1 trying to change User 2's password
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await service.ChangePassword(user2.Id, "Pass123!", "NewPass456!", user1.Id, CancellationToken.None)
        );
    }

    [Fact]
    public async Task AdminResetPassword_ByAdmin_SucceedsWithoutCurrentPassword()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        var admin = await service.CreateFirstAdmin("admin@test.com", "AdminPass123!", "Admin", CancellationToken.None);
        var user = await service.CreateUser("user@test.com", "OldPass123!", "User", false, "en-US", "UTC", admin.Id, CancellationToken.None);

        // Act - Admin resets user password without knowing current password
        await service.AdminResetPassword(user.Id, "NewAdminSetPass123!", admin.Id, CancellationToken.None);

        // Assert
        var updatedUser = await service.Get(user.Id, CancellationToken.None);
        Assert.NotNull(updatedUser);
        Assert.True(passwordHasher.Verify("NewAdminSetPass123!", updatedUser!.PasswordHash));
    }

    [Fact]
    public async Task AdminResetPassword_ByNonAdmin_ThrowsUnauthorizedException()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        var admin = await service.CreateFirstAdmin("admin@test.com", "AdminPass123!", "Admin", CancellationToken.None);
        var user1 = await service.CreateUser("user1@test.com", "Pass123!", "User 1", false, "en-US", "UTC", admin.Id, CancellationToken.None);
        var user2 = await service.CreateUser("user2@test.com", "Pass123!", "User 2", false, "en-US", "UTC", admin.Id, CancellationToken.None);

        // Act & Assert - Non-admin trying to reset password
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await service.AdminResetPassword(user2.Id, "HackedPass!", user1.Id, CancellationToken.None)
        );
    }

    [Fact]
    public async Task PasswordHasher_DifferentPasswordsProduceDifferentHashes()
    {
        // Arrange
        var passwordHasher = new Argon2IdPasswordHasher();

        // Act
        var hash1 = passwordHasher.Hash("Password123!");
        var hash2 = passwordHasher.Hash("Password456!");
        var hash3 = passwordHasher.Hash("Password123!"); // Same password as hash1

        // Assert
        Assert.NotEqual(hash1, hash2); // Different passwords = different hashes
        Assert.NotEqual(hash1, hash3); // Same password should produce different salt/hash (security)
        
        // But verification should work for all
        Assert.True(passwordHasher.Verify("Password123!", hash1));
        Assert.True(passwordHasher.Verify("Password456!", hash2));
        Assert.True(passwordHasher.Verify("Password123!", hash3));
    }

    [Fact]
    public async Task Delete_UserCannotDeleteThemselves()
    {
        // Arrange
        using var context = CreateContext();
        var passwordHasher = new Argon2IdPasswordHasher();
        var logger = Mock.Of<ILogger<UserService>>();
        var emailSender = Mock.Of<IEmailSender>();
        var service = new UserService(context, passwordHasher, logger, emailSender);

        var admin = await service.CreateFirstAdmin("admin@test.com", "AdminPass123!", "Admin", CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.Delete(admin.Id, admin.Id, CancellationToken.None)
        );
    }
}

