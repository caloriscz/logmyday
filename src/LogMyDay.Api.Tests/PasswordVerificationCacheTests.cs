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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Basic auth re-sends credentials on every request, so successful verifications are cached to
/// avoid re-running Argon2id each time. These cover the properties that make that safe: a cache
/// entry must be reachable only by the exact password that produced it, only for that user, and
/// only while the stored hash is unchanged.
/// </summary>
public class PasswordVerificationCacheTests
{
    private const string Password = "correct horse battery staple";
    private const string Hash = "argon2id$v=19$m=65536,t=3,p=4$c2FsdA==$aGFzaA==";

    private static PasswordVerificationCache NewCache() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void NotVerified_BeforeAnythingIsRecorded()
    {
        var cache = NewCache();

        Assert.False(cache.IsVerified(Guid.NewGuid(), Hash, Password));
    }

    [Fact]
    public void Verified_AfterRecordingTheSameCredential()
    {
        var cache = NewCache();
        var userId = Guid.NewGuid();

        cache.Record(userId, Hash, Password);

        Assert.True(cache.IsVerified(userId, Hash, Password));
    }

    [Fact]
    public void NotVerified_ForADifferentPassword()
    {
        var cache = NewCache();
        var userId = Guid.NewGuid();

        cache.Record(userId, Hash, Password);

        // A wrong guess must never ride in on someone else's successful verification.
        Assert.False(cache.IsVerified(userId, Hash, "wrong password"));
    }

    [Fact]
    public void NotVerified_ForADifferentUser()
    {
        var cache = NewCache();

        cache.Record(Guid.NewGuid(), Hash, Password);

        Assert.False(cache.IsVerified(Guid.NewGuid(), Hash, Password));
    }

    [Fact]
    public void NotVerified_OnceTheStoredHashChanges()
    {
        var cache = NewCache();
        var userId = Guid.NewGuid();

        cache.Record(userId, Hash, Password);

        // A password change replaces salt and hash, so the old entry is unreachable and the new
        // credential is re-derived on the very next request.
        const string rotatedHash = "argon2id$v=19$m=65536,t=3,p=4$bmV3c2FsdA==$bmV3aGFzaA==";

        Assert.False(cache.IsVerified(userId, rotatedHash, Password));
    }
}

/// <summary>
/// End-to-end through BasicAuthHandler: the cache must remove the Argon2 cost from repeat requests
/// without ever letting a bad password skip it.
/// </summary>
public class BasicAuthHandlerVerificationCacheTests
{
    private const string Email = "user@test.com";
    private const string Password = "password";

    private static async Task<(int verifyCalls, bool lastSucceeded)> AuthenticateTwiceAsync(bool passwordIsCorrect)
    {
        var user = new User { Id = Guid.NewGuid(), Email = Email, PasswordHash = "argon2id$v=19$m=65536,t=3,p=4$c2FsdA==$aGFzaA==" };

        var userService = Mock.Of<IUserService>(s =>
            s.FindByEmail(Email, It.IsAny<CancellationToken>()) == Task.FromResult<User?>(user));

        var verifyCalls = 0;
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(() => { verifyCalls++; return passwordIsCorrect; });

        var tracker = new AuthAttemptTracker(new MemoryCache(new MemoryCacheOptions()), NullLogger<AuthAttemptTracker>.Instance);
        var verificationCache = new PasswordVerificationCache(new MemoryCache(new MemoryCacheOptions()));

        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

        AuthenticateResult? result = null;

        // Same cache and tracker across both calls — a fresh handler per request, as in the pipeline.
        for (var i = 0; i < 2; i++)
        {
            var handler = new BasicAuthHandler(
                options.Object, NullLoggerFactory.Instance, UrlEncoder.Default,
                userService, hasher.Object, tracker, verificationCache);

            var context = new DefaultHttpContext();
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Password}"));
            context.Request.Headers.Authorization = $"Basic {credentials}";

            await handler.InitializeAsync(new AuthenticationScheme("basic", "basic", typeof(BasicAuthHandler)), context);
            result = await handler.AuthenticateAsync();
        }

        return (verifyCalls, result!.Succeeded);
    }

    [Fact]
    public async Task CorrectPassword_DerivesOnceThenServesFromCache()
    {
        var (verifyCalls, succeeded) = await AuthenticateTwiceAsync(passwordIsCorrect: true);

        Assert.True(succeeded);
        Assert.Equal(1, verifyCalls);
    }

    [Fact]
    public async Task WrongPassword_DerivesEveryTime()
    {
        // Failures are never cached, so brute-force attempts keep paying the full Argon2id cost.
        var (verifyCalls, succeeded) = await AuthenticateTwiceAsync(passwordIsCorrect: false);

        Assert.False(succeeded);
        Assert.Equal(2, verifyCalls);
    }
}
