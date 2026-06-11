using System;
using Microsoft.Extensions.Logging;

namespace LogMyDay.App.Mobile.Services;

public interface IApiContext
{
    Uri? Server { get; }
    string? Username { get; }
    string? Password { get; }
    bool IsConfigured { get; }
    void Configure(Uri server, string username, string password);
    void Clear();
    event Action? Changed;
    event Action? AuthenticationExpired;
    void NotifyAuthenticationExpired();
}

public class ApiContext : IApiContext
{
    private readonly ILogger<ApiContext> _logger;

    public ApiContext(ILogger<ApiContext> logger)
    {
        _logger = logger;
    }

    public Uri? Server { get; private set; }
    public string? Username { get; private set; }
    public string? Password { get; private set; }

    public event Action? Changed;
    public event Action? AuthenticationExpired;

    public bool IsConfigured => Server != null && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    public void Configure(Uri server, string username, string password)
    {
        Server = server;
        Username = username;
        Password = password;
        Changed?.Invoke();

        // Never log credentials or credential metadata (e.g. password length).
        _logger.LogInformation("ApiContext configured for server {Server}. IsConfigured: {IsConfigured}", server, IsConfigured);
    }

    public void Clear()
    {
        Server = null;
        Username = null;
        Password = null;
        Changed?.Invoke();

        _logger.LogInformation("ApiContext cleared");
    }

    public void NotifyAuthenticationExpired()
    {
        _logger.LogInformation("ApiContext: authentication expired, notifying subscribers");
        AuthenticationExpired?.Invoke();
    }
}
