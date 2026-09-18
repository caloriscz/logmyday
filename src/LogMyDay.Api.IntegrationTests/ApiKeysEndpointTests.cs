using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// Self-service key management over REST: a session or Basic-auth caller can list, create and
/// revoke their own keys, and a key can never manage keys.
/// </summary>
public class ApiKeysEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Email = "test@example.com";
    private const string Password = "integration-test-password";

    private readonly CustomWebApplicationFactory _factory;

    public ApiKeysEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        GiveSeededUserARealPassword();
    }

    private void GiveSeededUserARealPassword()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = db.Users.First(u => u.Email == Email);
        user.PasswordHash = hasher.Hash(Password);
        db.SaveChanges();
    }

    private HttpClient BasicClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{Password}")));

        return client;
    }

    [Fact]
    public async Task Session_CanCreateListAndRevoke()
    {
        var client = BasicClient();

        var createResponse = await client.PostAsJsonAsync("/api/account/api-keys", new CreateApiKeyDto("laptop", "ReadWrite", null));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiKeyCreatedDto>();
        Assert.NotNull(created);
        Assert.StartsWith("lmd_", created.Token);
        Assert.Equal("laptop", created.Key.Name);
        Assert.Equal("ReadWrite", created.Key.Scope);

        var list = await client.GetFromJsonAsync<List<ApiKeyDto>>("/api/account/api-keys");
        Assert.NotNull(list);
        var listed = Assert.Single(list, k => k.Id == created.Key.Id);
        Assert.Null(listed.RevokedUtc);

        var revokeResponse = await client.DeleteAsync($"/api/account/api-keys/{created.Key.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        list = await client.GetFromJsonAsync<List<ApiKeyDto>>("/api/account/api-keys");
        Assert.NotNull(list?.Single(k => k.Id == created.Key.Id).RevokedUtc);
    }

    [Fact]
    public async Task ListedKeys_NeverIncludeTokenOrHash()
    {
        var client = BasicClient();
        var created = await (await client.PostAsJsonAsync("/api/account/api-keys", new CreateApiKeyDto("secret-check", "ReadOnly", null)))
            .Content.ReadFromJsonAsync<ApiKeyCreatedDto>();

        var raw = await client.GetStringAsync("/api/account/api-keys");

        Assert.DoesNotContain(created!.Token, raw);
        Assert.DoesNotContain("keyHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(created.Key.Prefix, raw);
    }

    [Fact]
    public async Task AKey_CannotManageKeys()
    {
        // Mint a key through the service, then try to use it against the management endpoints.
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LogMyDayDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == Email);
            var keys = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IApiKeyService>();
            token = (await keys.Create(user.Id, "escalation-attempt", ApiKeyScope.ReadWrite, null, CancellationToken.None)).Token;
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/api-keys")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/api-keys", new CreateApiKeyDto("another", "ReadWrite", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/account/api-keys/1")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_Is401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/account/api-keys");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "ReadWrite")]
    [InlineData("valid name", "Admin")]
    [InlineData("valid name", "")]
    public async Task Create_RejectsBadInput(string name, string scope)
    {
        var response = await BasicClient().PostAsJsonAsync("/api/account/api-keys", new CreateApiKeyDto(name, scope, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsPastExpiry()
    {
        var response = await BasicClient().PostAsJsonAsync("/api/account/api-keys",
            new CreateApiKeyDto("expired at birth", "ReadOnly", DateTime.UtcNow.AddDays(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void RefitClient_ForTheBlazorUi_Resolves()
    {
        // The Profile page's ApiKeysPanel injects this; a broken registration would only surface at render.
        using var scope = _factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<LogMyDay.Shared.Interfaces.IApiKeysApi>());
    }

    [Fact]
    public async Task Revoke_UnknownId_Is404()
    {
        var response = await BasicClient().DeleteAsync("/api/account/api-keys/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
