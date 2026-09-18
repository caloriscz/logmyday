using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using LogMyDay.Api.Authentication;
using LogMyDay.App.Extensions;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Http;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// The rate limiter now runs after authentication. The existing endpoint policies must still
/// apply, and the new MCP policy must partition by API key rather than sharing one bucket.
/// </summary>
public class RateLimitingTests
{
    // --- MCP partition key ---

    private static DefaultHttpContext ContextWithKey(string? keyId, string ip = "10.0.0.1")
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (keyId != null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ApiKeyAuthDefaults.KeyIdClaim, keyId) }, ApiKeyAuthDefaults.SchemeName));
        }

        return context;
    }

    [Fact]
    public void McpPartition_IsPerKey()
    {
        var a = RateLimitingExtensions.McpPartitionKey(ContextWithKey("1"));
        var b = RateLimitingExtensions.McpPartitionKey(ContextWithKey("2"));
        var aAgain = RateLimitingExtensions.McpPartitionKey(ContextWithKey("1", ip: "192.168.1.9"));

        Assert.NotEqual(a, b);
        // Same key from a different address is still the same budget: the key is what we meter.
        Assert.Equal(a, aAgain);
    }

    [Fact]
    public void McpPartition_Unauthenticated_IsPerAddress_AndNeverCollidesWithAKey()
    {
        var anon1 = RateLimitingExtensions.McpPartitionKey(ContextWithKey(null, ip: "10.0.0.1"));
        var anon2 = RateLimitingExtensions.McpPartitionKey(ContextWithKey(null, ip: "10.0.0.2"));
        var keyed = RateLimitingExtensions.McpPartitionKey(ContextWithKey("1", ip: "10.0.0.1"));

        Assert.NotEqual(anon1, anon2);
        Assert.NotEqual(anon1, keyed);
    }

    // --- Existing endpoint policy still enforced after the reorder ---

    [Fact]
    public async Task AuthLoginPolicy_StillReturns429_AfterMovingTheLimiter()
    {
        // A fresh host so this test's ten permits are its own and no other test sees the 429.
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var body = new LoginDto("nobody@example.com", "wrong-password");

        HttpStatusCode last = default;
        for (var i = 0; i < 11; i++)
        {
            last = (await client.PostAsJsonAsync("/api/auth/login", body)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }
}
