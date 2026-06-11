using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Controllers;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Cookie login path tests focused on the progressive lockout shared with Basic Auth.
/// </summary>
public class AuthControllerTests
{
    private const string Email = "user@test.com";
    private const string Password = "correct horse battery staple";

    // DefaultHttpContext has no remote IP, so the identifier mirrors BuildAttemptIdentifier("unknown").
    private static readonly string Identifier = $"unknown:{Email}";

    private static AuthAttemptTracker CreateTracker()
        => new(new MemoryCache(new MemoryCacheOptions()), Mock.Of<ILogger<AuthAttemptTracker>>());

    private static AuthController CreateController(
        AuthAttemptTracker tracker,
        IUserService userService,
        IPasswordHasher passwordHasher)
    {
        return new AuthController(
            userService,
            Mock.Of<IAuthService>(),
            passwordHasher,
            Mock.Of<IAntiforgery>(),
            tracker,
            Mock.Of<ILogger<AuthController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    // Rejects every credential — drives the failure path.
    private static IUserService FailingUserService()
        => Mock.Of<IUserService>(s => s.FindByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult<User?>(null));

    // Accepts the test credentials — drives the success path.
    private static (IUserService userService, IPasswordHasher hasher) PassingDependencies()
    {
        var user = new User { Id = Guid.NewGuid(), Email = Email, PasswordHash = "hash" };
        var userService = Mock.Of<IUserService>(s =>
            s.FindByEmail(Email, It.IsAny<CancellationToken>()) == Task.FromResult<User?>(user));
        var hasher = Mock.Of<IPasswordHasher>(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()) == true);

        return (userService, hasher);
    }

    [Fact]
    public async Task Login_ReturnsTooManyRequests_AfterMaxFailedAttempts()
    {
        var tracker = CreateTracker();
        var controller = CreateController(tracker, FailingUserService(), Mock.Of<IPasswordHasher>());

        // Five failures trip the progressive lockout (matching Basic Auth's threshold).
        for (var i = 0; i < 5; i++)
        {
            var failure = await controller.Login(new LoginDto(Email, "bad"), CancellationToken.None);
            Assert.Equal(401, Assert.IsType<ObjectResult>(failure).StatusCode);
        }

        var blocked = await controller.Login(new LoginDto(Email, "bad"), CancellationToken.None);

        Assert.Equal(429, Assert.IsType<ObjectResult>(blocked).StatusCode);
    }

    [Fact]
    public async Task LoginForm_RedirectsToLockoutError_AfterMaxFailedAttempts()
    {
        var tracker = CreateTracker();
        var controller = CreateController(tracker, FailingUserService(), Mock.Of<IPasswordHasher>());

        for (var i = 0; i < 5; i++)
        {
            await controller.LoginForm(Email, "bad");
        }

        var blocked = await controller.LoginForm(Email, "bad");

        var redirect = Assert.IsType<RedirectResult>(blocked);
        Assert.Contains("Too many failed attempts", redirect.Url);
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsFailureCounter()
    {
        var tracker = CreateTracker();
        var (userService, hasher) = PassingDependencies();

        // Four failures stay under the threshold...
        var failController = CreateController(tracker, FailingUserService(), Mock.Of<IPasswordHasher>());
        for (var i = 0; i < 4; i++)
        {
            await failController.Login(new LoginDto(Email, "bad"), CancellationToken.None);
        }

        // ...a success clears them, so the same shared identifier is no longer near lockout.
        var successController = CreateController(tracker, userService, hasher);
        var success = await successController.Login(new LoginDto(Email, Password), CancellationToken.None);
        Assert.IsType<NoContentResult>(success);
        Assert.False(tracker.IsBlocked(Identifier));

        // Four further failures restart from zero rather than tipping over the previous four.
        for (var i = 0; i < 4; i++)
        {
            await failController.Login(new LoginDto(Email, "bad"), CancellationToken.None);
        }

        Assert.False(tracker.IsBlocked(Identifier));
    }

    [Fact]
    public async Task Login_LockoutIsSharedAcrossAuthSchemes_ForSameAccount()
    {
        // The controller and Basic Auth use one AuthAttemptTracker with the same identifier shape,
        // so failures on the cookie path block the account on the shared tracker.
        var tracker = CreateTracker();
        var controller = CreateController(tracker, FailingUserService(), Mock.Of<IPasswordHasher>());

        for (var i = 0; i < 5; i++)
        {
            await controller.Login(new LoginDto(Email, "bad"), CancellationToken.None);
        }

        Assert.True(tracker.IsBlocked(Identifier));
    }
}
