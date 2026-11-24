using System.Net;
using System.Net.Http.Json;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LogMyDay.Api.IntegrationTests;

/// <summary>
/// The sign-in page shows fixed texts by key only, so a link can't put words of its own on it, and
/// it sends a visitor who is signed in on to the app.
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
    [InlineData("error=invalid", "Invalid email or password.")]
    [InlineData("error=blocked", "Too many failed attempts. Please try again later.")]
    [InlineData("signedOut=password", "Your password was changed. Sign in with the new password.")]
    [InlineData("signedOut=session", "Your session ended. Sign in again.")]
    public async Task AKnownKey_ShowsItsText(string query, string text)
    {
        var page = await Browser().GetStringAsync($"/login?{query}");

        Assert.Contains(text, page);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("signedOut")]
    public async Task AnyOtherValue_ShowsNothingOfIt(string key)
    {
        var page = await Browser().GetStringAsync($"/login?{key}=Your%20account%20is%20locked%2C%20call%20555-0100");

        Assert.DoesNotContain("call 555-0100", page);
    }

    [Fact]
    public async Task AVisitorWhoIsSignedIn_IsSentToTheApp()
    {
        var browser = Browser();
        var login = new LoginDto(CustomWebApplicationFactory.TestUserEmail, CustomWebApplicationFactory.TestUserPassword);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/api/auth/login", login)).StatusCode);

        var response = await browser.GetAsync("/login?signedOut=session");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.AbsolutePath ?? response.Headers.Location?.OriginalString);
    }

    /// <summary>A client that keeps its cookies and doesn't follow redirects. The cookie is Secure, so it talks https.</summary>
    private HttpClient Browser()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }
}
