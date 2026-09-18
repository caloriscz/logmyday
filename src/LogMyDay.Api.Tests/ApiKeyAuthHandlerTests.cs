using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LogMyDay.Api.Tests;

/// <summary>
/// The handler must: ignore anything that is not our token, refuse every bad key identically and
/// feed the lockout tracker, and on success emit exactly the claims the other schemes emit plus
/// the key-specific ones the MCP policies read.
/// </summary>
public class ApiKeyAuthHandlerTests
{
    private const string ValidToken = "lmd_" + "abcdefgh" + "ijklmnopqrstuvwxyz0123456789ABCDEFG";
    private const string Prefix = "lmd_abcdefgh";

    private static readonly User TestUser = new()
    {
        Id = Guid.NewGuid(),
        Email = "user@test.com",
        DisplayName = "Test User",
        PasswordHash = "hash",
        IsAdmin = false
    };

    private readonly Mock<IApiKeyService> _apiKeys = new();
    private readonly AuthAttemptTracker _tracker = new(new MemoryCache(new MemoryCacheOptions()), NullLogger<AuthAttemptTracker>.Instance);

    private async Task<(AuthenticateResult Result, DefaultHttpContext Context)> AuthenticateAsync(string? authorizationHeader)
    {
        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

        var handler = new ApiKeyAuthHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, _apiKeys.Object, _tracker);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        if (authorizationHeader != null)
        {
            context.Request.Headers.Authorization = authorizationHeader;
        }

        await handler.InitializeAsync(new AuthenticationScheme(ApiKeyAuthDefaults.SchemeName, null, typeof(ApiKeyAuthHandler)), context);
        var result = await handler.AuthenticateAsync();

        return (result, context);
    }

    private void SetupValidKey(ApiKeyScope scope = ApiKeyScope.ReadWrite, User? user = null)
    {
        _apiKeys.Setup(s => s.Validate(ValidToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiKeyValidation(user ?? TestUser, 42, Prefix, scope));
    }

    // --- Not ours: defer to other schemes ---

    [Theory]
    [InlineData(null)]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Bearer some-oauth-token")]
    [InlineData("Bearer tsk_notours")]
    public async Task NotOurToken_ReturnsNoResult_AndNeverValidates(string? header)
    {
        var (result, _) = await AuthenticateAsync(header);

        Assert.True(result.None);
        _apiKeys.Verify(s => s.Validate(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Success ---

    [Fact]
    public async Task ValidKey_Succeeds_WithStandardAndKeyClaims()
    {
        SetupValidKey(ApiKeyScope.ReadWrite);

        var (result, context) = await AuthenticateAsync("Bearer " + ValidToken);

        Assert.True(result.Succeeded);
        var p = result.Principal!;
        Assert.Equal(TestUser.Id.ToString(), p.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(TestUser.Email, p.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("Test User", p.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("false", p.FindFirst("is_admin")?.Value);
        Assert.Equal(ApiKeyAuthDefaults.AuthSourceValue, p.FindFirst(ApiKeyAuthDefaults.AuthSourceClaim)?.Value);
        Assert.Equal(ApiKeyAuthDefaults.ScopeWrite, p.FindFirst(ApiKeyAuthDefaults.ScopeClaim)?.Value);
        Assert.Equal("42", p.FindFirst(ApiKeyAuthDefaults.KeyIdClaim)?.Value);
        Assert.Equal(Prefix, p.FindFirst(ApiKeyAuthDefaults.KeyPrefixClaim)?.Value);
        Assert.Equal(ApiKeyAuthDefaults.SchemeName, context.Items[BasicAuthHandler.SchemeItemKey]);
    }

    [Fact]
    public async Task ReadOnlyKey_EmitsReadScope()
    {
        SetupValidKey(ApiKeyScope.ReadOnly);

        var (result, _) = await AuthenticateAsync("Bearer " + ValidToken);

        Assert.Equal(ApiKeyAuthDefaults.ScopeRead, result.Principal!.FindFirst(ApiKeyAuthDefaults.ScopeClaim)?.Value);
    }

    [Fact]
    public async Task AdminUser_EmitsIsAdminTrue()
    {
        SetupValidKey(user: new User { Id = Guid.NewGuid(), Email = "admin@test.com", PasswordHash = "h", IsAdmin = true });

        var (result, _) = await AuthenticateAsync("Bearer " + ValidToken);

        Assert.Equal("true", result.Principal!.FindFirst("is_admin")?.Value);
    }

    [Fact]
    public async Task ClaimSet_MatchesBasicAuthHandler_ForTheSameUser()
    {
        // The AdminOnly policy and every user-scoped controller read these four claims; a key must
        // look exactly like a password login to them.
        SetupValidKey();
        var (viaKey, _) = await AuthenticateAsync("Bearer " + ValidToken);
        var viaBasic = await AuthenticateWithBasicAsync(TestUser);

        foreach (var type in new[] { ClaimTypes.NameIdentifier, ClaimTypes.Email, ClaimTypes.Name, "is_admin" })
        {
            Assert.Equal(viaBasic.Principal!.FindFirst(type)?.Value, viaKey.Principal!.FindFirst(type)?.Value);
        }
    }

    // --- Failure ---

    [Fact]
    public async Task InvalidKey_Fails_AndRecordsAttempt()
    {
        _apiKeys.Setup(s => s.Validate(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ApiKeyValidation?)null);

        var (result, _) = await AuthenticateAsync("Bearer " + ValidToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid API key", result.Failure?.Message);
        // One failure is not a lockout; five are. Prove the attempt was counted by reaching it.
        for (var i = 0; i < 4; i++)
        {
            await AuthenticateAsync("Bearer " + ValidToken);
        }
        Assert.True(_tracker.IsBlocked($"127.0.0.1:{Prefix}"));
    }

    [Fact]
    public async Task BlockedPrefix_Fails_WithoutConsultingTheStore()
    {
        for (var i = 0; i < 5; i++)
        {
            _tracker.RecordFailedAttempt($"127.0.0.1:{Prefix}");
        }
        SetupValidKey();

        var (result, _) = await AuthenticateAsync("Bearer " + ValidToken);

        Assert.False(result.Succeeded);
        _apiKeys.Verify(s => s.Validate(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Challenge_AdvertisesBearer()
    {
        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());
        var handler = new ApiKeyAuthHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, _apiKeys.Object, _tracker);
        var context = new DefaultHttpContext();
        await handler.InitializeAsync(new AuthenticationScheme(ApiKeyAuthDefaults.SchemeName, null, typeof(ApiKeyAuthHandler)), context);

        await handler.ChallengeAsync(null);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.StartsWith("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
    }

    private static async Task<AuthenticateResult> AuthenticateWithBasicAsync(User user)
    {
        var userService = Mock.Of<IUserService>(s => s.FindByEmail(user.Email, It.IsAny<CancellationToken>()) == Task.FromResult<User?>(user));
        var hasher = Mock.Of<IPasswordHasher>(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()) == true);
        var tracker = new AuthAttemptTracker(new MemoryCache(new MemoryCacheOptions()), NullLogger<AuthAttemptTracker>.Instance);
        var cache = new PasswordVerificationCache(new MemoryCache(new MemoryCacheOptions()));
        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

        var handler = new BasicAuthHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, userService, hasher, tracker, cache);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user.Email}:password"));
        await handler.InitializeAsync(new AuthenticationScheme("basic", "basic", typeof(BasicAuthHandler)), context);

        return await handler.AuthenticateAsync();
    }
}
