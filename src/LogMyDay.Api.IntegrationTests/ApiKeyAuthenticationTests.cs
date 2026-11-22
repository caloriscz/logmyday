using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// The api-key scheme wired end to end: the smart-auth selector routes "Bearer lmd_…" to it, a real
/// request authenticates, and the MCP policies admit exactly the principals they should.
/// </summary>
public class ApiKeyAuthenticationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiKeyAuthenticationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(string Token, int KeyId, Guid UserId)> CreateKeyAsync(ApiKeyScope scope, string email = CustomWebApplicationFactory.TestUserEmail)
    {
        using var scopeSvc = _factory.Services.CreateScope();
        var db = scopeSvc.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        var user = await TestPlatform.For(db).Users.FirstAsync(u => u.Email == email);
        var keys = scopeSvc.ServiceProvider.GetRequiredService<IApiKeyService>();
        var created = await keys.Create(user.Id, $"test-{Guid.NewGuid():N}", scope, null, CancellationToken.None);

        return (created.Token, created.Key.Id, user.Id);
    }

    private HttpClient ClientWithBearer(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    // --- Real requests ---

    [Fact]
    public async Task ValidKey_AuthenticatesAgainstADefaultSchemeEndpoint()
    {
        var (token, _, _) = await CreateKeyAsync(ApiKeyScope.ReadWrite);

        // /api/tags derives from BaseApiController: plain [Authorize], so smart-auth decides the scheme.
        var response = await ClientWithBearer(token).GetAsync("/api/tags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RevokedKey_IsRefused()
    {
        var (token, keyId, userId) = await CreateKeyAsync(ApiKeyScope.ReadWrite);
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IApiKeyService>().Revoke(userId, keyId, CancellationToken.None);
        }

        var response = await ClientWithBearer(token).GetAsync("/api/tags");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownLmdToken_Is401_WithBearerChallenge()
    {
        var response = await ClientWithBearer("lmd_" + new string('x', 43)).GetAsync("/api/tags");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task ForeignBearerToken_FallsThroughToCookie_And401s()
    {
        var response = await ClientWithBearer("some-other-provider-token").GetAsync("/api/tags");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SchemePinnedEndpoint_DoesNotAcceptKeys_InV1()
    {
        // /api/auth/me pins "lmd-cookie,basic": keys are MCP-only in v1 by design.
        var (token, _, _) = await CreateKeyAsync(ApiKeyScope.ReadWrite);

        var response = await ClientWithBearer(token).GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- A key's scope on REST ---

    [Fact]
    public async Task ReadOnlyKey_ReadsThroughRest_ButChangesNothing()
    {
        var client = ClientWithBearer(CustomWebApplicationFactory.ReadOnlyKeyToken);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tags")).StatusCode);

        await AssertReadOnlyRefusal(await client.DeleteAsync("/api/tags/999999"));
        await AssertReadOnlyRefusal(await client.PostAsJsonAsync("/api/tags", new { tagName = "Refused" }));

        // A controller outside BaseApiController.
        await AssertReadOnlyRefusal(await client.PatchAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { displayName = "Refused" }));
    }

    [Fact]
    public async Task ReadWriteKey_StillWritesThroughRest()
    {
        var response = await ClientWithBearer(CustomWebApplicationFactory.ReadWriteKeyToken).DeleteAsync("/api/tags/999999");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminsReadWriteKey_ManagesUsersAndSettings()
    {
        var client = ClientWithBearer(CustomWebApplicationFactory.AdminKeyToken);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings/ai")).StatusCode);
    }

    [Fact]
    public async Task AdminsReadOnlyKey_PassesNoAdminEndpoint()
    {
        var (token, _, _) = await CreateKeyAsync(ApiKeyScope.ReadOnly, CustomWebApplicationFactory.AdminUserEmail);
        var client = ClientWithBearer(token);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/settings/ai")).StatusCode);
    }

    [Theory]
    [InlineData(ApiKeyScope.ReadWrite)]
    [InlineData(ApiKeyScope.ReadOnly)]
    public async Task NoKey_ReachesDatabaseFiles_OrTheInstallation(ApiKeyScope scope)
    {
        var (token, _, adminId) = await CreateKeyAsync(scope, CustomWebApplicationFactory.AdminUserEmail);
        var client = ClientWithBearer(token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/backup/export/installation")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/backup/database/{adminId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"/api/backup/database/{adminId}/restore", new MultipartFormDataContent())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/backup/clear")).StatusCode);
    }

    private static async Task AssertReadOnlyRefusal(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(ReadOnlyApiKeyFilter.Message, await response.Content.ReadAsStringAsync());
    }

    // --- Selector: the Basic path is untouched ---

    [Theory]
    [InlineData("Basic dXNlcjpwYXNz", "basic")]
    [InlineData("Bearer lmd_anything", ApiKeyAuthDefaults.SchemeName)]
    [InlineData("bearer LMD_case-insensitive", ApiKeyAuthDefaults.SchemeName)]
    [InlineData("Bearer tsk_taskino", "lmd-cookie")]
    [InlineData(null, "lmd-cookie")]
    [InlineData(null, ApiKeyAuthDefaults.SchemeName, "/mcp")]
    [InlineData("Bearer tsk_taskino", ApiKeyAuthDefaults.SchemeName, "/mcp")]
    [InlineData("Basic dXNlcjpwYXNz", "basic", "/mcp")]
    [InlineData(null, "lmd-cookie", "/mcp-not-the-endpoint")]
    public void SmartAuthSelector_RoutesByHeaderShape(string? header, string expectedScheme, string path = "/api/tags")
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>().Get("smart-auth");
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (header != null)
        {
            context.Request.Headers.Authorization = header;
        }

        Assert.Equal(expectedScheme, options.ForwardDefaultSelector!(context));
    }

    // --- Policies ---

    private static ClaimsPrincipal Principal(string source, string? scope, bool isAdmin)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, "p@test.com"),
            new("is_admin", isAdmin ? "true" : "false")
        };
        if (source == ApiKeyAuthDefaults.AuthSourceValue)
        {
            claims.Add(new Claim(ApiKeyAuthDefaults.AuthSourceClaim, source));
            claims.Add(new Claim(ApiKeyAuthDefaults.ScopeClaim, scope!));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, source));
    }

    private async Task<bool> Authorized(ClaimsPrincipal principal, string policy)
    {
        var auth = _factory.Services.GetRequiredService<IAuthorizationService>();
        var result = await auth.AuthorizeAsync(principal, null, policy);

        return result.Succeeded;
    }

    [Fact]
    public async Task McpRead_AdmitsAnyKey_RejectsCookieSessions()
    {
        Assert.True(await Authorized(Principal("api-key", "read", false), McpPolicies.Read));
        Assert.True(await Authorized(Principal("api-key", "write", false), McpPolicies.Read));
        // An admin's browser session is still a browser session: no.
        Assert.False(await Authorized(Principal("lmd-cookie", null, true), McpPolicies.Read));
        Assert.False(await Authorized(new ClaimsPrincipal(new ClaimsIdentity()), McpPolicies.Read));
    }

    [Fact]
    public async Task McpWrite_RequiresWriteScope()
    {
        Assert.True(await Authorized(Principal("api-key", "write", false), McpPolicies.Write));
        Assert.False(await Authorized(Principal("api-key", "read", false), McpPolicies.Write));
        Assert.False(await Authorized(Principal("lmd-cookie", null, true), McpPolicies.Write));
    }

    [Fact]
    public async Task McpAdmin_RequiresAdminUserAndWriteScope()
    {
        Assert.True(await Authorized(Principal("api-key", "write", true), McpPolicies.Admin));
        // Admin with a read-only key: the key's scope wins.
        Assert.False(await Authorized(Principal("api-key", "read", true), McpPolicies.Admin));
        // Write key, ordinary user.
        Assert.False(await Authorized(Principal("api-key", "write", false), McpPolicies.Admin));
        Assert.False(await Authorized(Principal("lmd-cookie", null, true), McpPolicies.Admin));
    }

    [Fact]
    public async Task AdminOnly_AdmitsAdminSessionsAndReadWriteKeys_NotReadOnlyKeys()
    {
        Assert.True(await Authorized(Principal("lmd-cookie", null, true), "AdminOnly"));
        Assert.True(await Authorized(Principal("api-key", "write", true), "AdminOnly"));
        // Admin with a read-only key: the key's scope wins, for reads too.
        Assert.False(await Authorized(Principal("api-key", "read", true), "AdminOnly"));
        Assert.False(await Authorized(Principal("api-key", "write", false), "AdminOnly"));
        Assert.False(await Authorized(Principal("lmd-cookie", null, false), "AdminOnly"));
    }
}
