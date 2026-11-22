using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// The installation archive holds every user's data and password hashes, and the admin endpoints
/// reach other users' database files, so only an admin may call them. The files themselves are
/// covered by DatabaseBackupsTests: this host keeps its data in memory.
/// </summary>
public class DatabaseBackupEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Endpoint = "/api/backup/export/installation";

    private readonly CustomWebApplicationFactory _factory;

    public DatabaseBackupEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnotherUsersDatabase_IsForbiddenToANonAdmin()
    {
        var client = _factory.CreateClient();
        var credentials = $"{CustomWebApplicationFactory.TestUserEmail}:{CustomWebApplicationFactory.TestUserPassword}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        var someone = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/backup/database/{someone}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/backup/database/{someone}/restore", new MultipartFormDataContent())).StatusCode);
    }

    [Fact]
    public async Task OwnDatabase_NeedsASignedInUser()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/backup/database")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/backup/database/restore", new MultipartFormDataContent())).StatusCode);
    }

    [Fact]
    public async Task NonAdmin_IsForbidden()
    {
        var client = _factory.CreateClient();
        var credentials = $"{CustomWebApplicationFactory.TestUserEmail}:{CustomWebApplicationFactory.TestUserPassword}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));

        var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
