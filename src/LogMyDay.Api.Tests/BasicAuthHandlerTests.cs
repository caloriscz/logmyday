using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Verifies BasicAuthHandler emits the same claim set as the cookie handler so the
/// AdminOnly policy is satisfiable over Basic Auth (mobile).
/// </summary>
public class BasicAuthHandlerTests
{
    private const string Email = "user@test.com";
    private const string Password = "password";

    private static async Task<AuthenticateResult> AuthenticateAsync(bool isAdmin)
    {
        var user = new User { Id = Guid.NewGuid(), Email = Email, PasswordHash = "hash", IsAdmin = isAdmin };

        var userService = Mock.Of<IUserService>(s =>
            s.FindByEmail(Email, It.IsAny<CancellationToken>()) == Task.FromResult<User?>(user));
        var hasher = Mock.Of<IPasswordHasher>(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()) == true);
        var tracker = new AuthAttemptTracker(new MemoryCache(new MemoryCacheOptions()), NullLogger<AuthAttemptTracker>.Instance);

        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

        var handler = new BasicAuthHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, userService, hasher, tracker);

        var context = new DefaultHttpContext();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Password}"));
        context.Request.Headers.Authorization = $"Basic {credentials}";

        await handler.InitializeAsync(new AuthenticationScheme("basic", "basic", typeof(BasicAuthHandler)), context);

        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task AdminUser_EmitsIsAdminTrue_SatisfyingAdminOnly()
    {
        var result = await AuthenticateAsync(isAdmin: true);

        Assert.True(result.Succeeded);
        // AdminOnly is RequireClaim("is_admin", "true"); the claim must be present with this value.
        Assert.Equal("true", result.Principal!.FindFirst("is_admin")?.Value);
    }

    [Fact]
    public async Task NonAdminUser_EmitsIsAdminFalse_DeniedAdminOnly()
    {
        var result = await AuthenticateAsync(isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal("false", result.Principal!.FindFirst("is_admin")?.Value);
    }
}
