using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// The sign-in page shows fixed texts by key only, so a link can't put words of its own on it.
/// </summary>
public class SignInPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SignInPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AFailedFormSignIn_RedirectsWithAKey_NotAText()
    {
        var browser = Browser();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = $"nobody-{Guid.NewGuid():N}@example.com",
            ["password"] = "not-the-password",
        });

        var response = await browser.PostAsync("/api/auth/login-form", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?error=invalid", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("invalid", "Invalid email or password.")]
    [InlineData("blocked", "Too many failed attempts. Please try again later.")]
    [InlineData("failed", "An error occurred during login.")]
    public async Task AKnownKey_ShowsItsText(string key, string text)
    {
        var page = await Browser().GetStringAsync($"/login?error={key}");

        Assert.Contains(text, page);
    }

    [Fact]
    public async Task AnyOtherValue_ShowsNothingOfIt()
    {
        var page = await Browser().GetStringAsync("/login?error=Your%20account%20is%20locked%2C%20call%20555-0100");

        Assert.DoesNotContain("call 555-0100", page);
    }

    /// <summary>A client that doesn't follow redirects.</summary>
    private HttpClient Browser()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }
}
