using System.Net;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// Verifies empty-body error responses on API routes are filled with a consistent ProblemDetails
/// by the status-code-pages middleware (rather than blank bodies).
/// </summary>
public class ApiErrorResponseTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiErrorResponseTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnmatchedApiRoute_Returns404_AsProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AnIdThatDoesNotExist_Returns404_WithTheServicesMessage()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CustomWebApplicationFactory.ReadWriteKeyToken);

        // The units controller has no not-found branch of its own; the service throws.
        var response = await client.GetAsync("/api/units/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(("Unit not found", 404), (problem.GetProperty("title").GetString(), problem.GetProperty("status").GetInt32()));
    }

    [Fact]
    public async Task UnauthenticatedApiRequest_Returns401_AsProblemDetails()
    {
        var client = _factory.CreateClient();

        // /api/auth/me requires authentication; cookie auth returns 401 (not a redirect) for /api.
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
