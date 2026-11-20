using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// The whole-database SQLite export holds every user's data and password hashes, so only an admin
/// may download it. The export itself is covered by SqliteExportServiceTests: this host runs on
/// the in-memory provider, which has no migrations to check.
/// </summary>
public class SqliteExportEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Endpoint = "/api/backup/export/sqlite";

    private readonly CustomWebApplicationFactory _factory;

    public SqliteExportEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
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
