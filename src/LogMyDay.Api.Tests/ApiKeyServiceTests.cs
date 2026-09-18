using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LogMyDay.Api.Tests;

/// <summary>
/// The properties that make API keys safe to hand to an agent: the token is never stored, only its
/// owner can revoke it, and Validate refuses everything that must not authenticate.
/// </summary>
public class ApiKeyServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));
    private readonly LogMyDayDbContext _context;
    private readonly ApiKeyService _service;
    private readonly Guid _userId;

    public ApiKeyServiceTests()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new LogMyDayDbContext(options);
        _userId = SeedUser("owner@test.com");
        _service = new ApiKeyService(_context, _time, NullLogger<ApiKeyService>.Instance);
    }

    private Guid SeedUser(string email)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hash",
            Culture = "en-US",
            TimeZone = "Europe/Vienna"
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        return user.Id;
    }

    private Task<Shared.DTOs.ApiKeyCreatedDto> CreateKey(ApiKeyScope scope = ApiKeyScope.ReadWrite, DateTime? expires = null, string name = "laptop")
        => _service.Create(_userId, name, scope, expires, CancellationToken.None);

    // --- Token shape and storage ---

    [Fact]
    public async Task Create_TokenHasExpectedShape()
    {
        var created = await CreateKey();

        Assert.StartsWith("lmd_", created.Token);
        Assert.Equal(ApiKeyService.TokenLength, created.Token.Length);
        // base64url: no padding, no '+' or '/'
        Assert.DoesNotContain("=", created.Token);
        Assert.DoesNotContain("+", created.Token);
        Assert.DoesNotContain("/", created.Token);
    }

    [Fact]
    public async Task Create_StoresHashAndPrefix_NeverTheToken()
    {
        var created = await CreateKey();
        var row = await _context.ApiKeys.SingleAsync();

        Assert.Equal(created.Token[..ApiKeyService.PrefixLength], row.Prefix);
        Assert.Equal(ApiKeyService.Hash(created.Token), row.KeyHash);
        Assert.NotEqual(created.Token, row.KeyHash);
        Assert.DoesNotContain(created.Token, row.KeyHash);
    }

    [Fact]
    public async Task Create_TwoKeys_HaveDifferentTokens()
    {
        var a = await CreateKey(name: "a");
        var b = await CreateKey(name: "b");

        Assert.NotEqual(a.Token, b.Token);
    }

    [Fact]
    public async Task Create_ReturnsDtoWithoutHash()
    {
        var created = await CreateKey(ApiKeyScope.ReadOnly, name: "phone");

        Assert.Equal("phone", created.Key.Name);
        Assert.Equal("ReadOnly", created.Key.Scope);
        Assert.Equal(Now, created.Key.CreatedUtc);
        Assert.Null(created.Key.RevokedUtc);
        Assert.Null(created.Key.LastUsedUtc);
    }

    // --- Create validation ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsEmptyName(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateKey(name: name));
    }

    [Fact]
    public async Task Create_RejectsOverlongName()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateKey(name: new string('x', ApiKeyService.MaxNameLength + 1)));
    }

    [Fact]
    public async Task Create_RejectsExpiryInThePast()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateKey(expires: Now.AddMinutes(-1)));
    }

    [Fact]
    public async Task Create_EnforcesActiveKeyLimit_IgnoringRevokedAndExpired()
    {
        for (var i = 0; i < ApiKeyService.MaxActiveKeysPerUser; i++)
        {
            await CreateKey(name: $"k{i}");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateKey(name: "one too many"));

        // Revoking one frees a slot.
        var first = await _context.ApiKeys.FirstAsync();
        await _service.Revoke(_userId, first.Id, CancellationToken.None);

        await CreateKey(name: "fits again");
    }

    // --- List ---

    [Fact]
    public async Task List_ReturnsOnlyOwnKeys_NewestFirst_WithoutHash()
    {
        await CreateKey(name: "older");
        _time.Advance(TimeSpan.FromMinutes(1));
        await CreateKey(name: "newer");

        var otherUser = SeedUser("other@test.com");
        await _service.Create(otherUser, "theirs", ApiKeyScope.ReadOnly, null, CancellationToken.None);

        var list = await _service.List(_userId, CancellationToken.None);

        Assert.Equal(2, list.Count);
        Assert.Equal("newer", list[0].Name);
        Assert.Equal("older", list[1].Name);
        Assert.DoesNotContain(list, k => k.Name == "theirs");
    }

    // --- Revoke ---

    [Fact]
    public async Task Revoke_SetsRevokedUtc_AndIsIdempotent()
    {
        var created = await CreateKey();

        await _service.Revoke(_userId, created.Key.Id, CancellationToken.None);
        _time.Advance(TimeSpan.FromHours(1));
        await _service.Revoke(_userId, created.Key.Id, CancellationToken.None);

        var row = await _context.ApiKeys.SingleAsync();
        Assert.Equal(Now, row.RevokedUtc);
    }

    [Fact]
    public async Task Revoke_AnotherUsersKey_IsNotFound()
    {
        var created = await CreateKey();
        var otherUser = SeedUser("other@test.com");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.Revoke(otherUser, created.Key.Id, CancellationToken.None));

        var row = await _context.ApiKeys.SingleAsync();
        Assert.Null(row.RevokedUtc);
    }

    // --- Validate ---

    [Fact]
    public async Task Validate_CorrectToken_ReturnsUserAndScope()
    {
        var created = await CreateKey(ApiKeyScope.ReadOnly);

        var result = await _service.Validate(created.Token, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(_userId, result.User.Id);
        Assert.Equal(ApiKeyScope.ReadOnly, result.Scope);
        Assert.Equal(created.Key.Id, result.KeyId);
        Assert.Equal(created.Key.Prefix, result.Prefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lmd_")]
    [InlineData("not-a-token")]
    [InlineData("tsk_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Validate_MalformedToken_ReturnsNull(string? token)
    {
        await CreateKey();

        Assert.Null(await _service.Validate(token!, CancellationToken.None));
    }

    [Fact]
    public async Task Validate_UnknownPrefix_ReturnsNull()
    {
        await CreateKey();
        var unknown = "lmd_" + new string('Z', ApiKeyService.TokenLength - 4);

        Assert.Null(await _service.Validate(unknown, CancellationToken.None));
    }

    [Fact]
    public async Task Validate_SamePrefixWrongRemainder_ReturnsNull()
    {
        // A guess that gets the visible prefix right must still fail on the hash.
        var created = await CreateKey();
        var forged = created.Token[..ApiKeyService.PrefixLength] + new string('A', ApiKeyService.TokenLength - ApiKeyService.PrefixLength);

        Assert.Null(await _service.Validate(forged, CancellationToken.None));
    }

    [Fact]
    public async Task Validate_RevokedKey_ReturnsNull()
    {
        var created = await CreateKey();
        await _service.Revoke(_userId, created.Key.Id, CancellationToken.None);

        Assert.Null(await _service.Validate(created.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Validate_ExpiredKey_ReturnsNull_ButWorksUntilThen()
    {
        var created = await CreateKey(expires: Now.AddHours(1));

        Assert.NotNull(await _service.Validate(created.Token, CancellationToken.None));

        _time.Advance(TimeSpan.FromHours(1));

        Assert.Null(await _service.Validate(created.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Validate_DeletedUser_ReturnsNull()
    {
        var created = await CreateKey();

        // The FK cascade removes keys with the user in a real database; the in-memory provider does
        // not, so this exercises the explicit user-null guard rather than the cascade.
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        Assert.Null(await _service.Validate(created.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Validate_RecordsLastUsed_ButNotOnEveryCall()
    {
        var created = await CreateKey();

        await _service.Validate(created.Token, CancellationToken.None);
        var row = await _context.ApiKeys.SingleAsync();
        Assert.Equal(Now, row.LastUsedUtc);

        // Inside the write interval: no update.
        _time.Advance(TimeSpan.FromMinutes(2));
        await _service.Validate(created.Token, CancellationToken.None);
        Assert.Equal(Now, row.LastUsedUtc);

        // Past it: updated.
        _time.Advance(TimeSpan.FromMinutes(4));
        await _service.Validate(created.Token, CancellationToken.None);
        Assert.Equal(Now.AddMinutes(6), row.LastUsedUtc);
    }
}
