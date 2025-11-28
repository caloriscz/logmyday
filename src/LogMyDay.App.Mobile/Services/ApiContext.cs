using System;

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
    public Uri? Server { get; private set; }
    public string? Username { get; private set; }
    public string? Password { get; private set; }

    public event Action? Changed;
    public event Action? AuthenticationExpired;

    public bool IsConfigured => Server != null && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    public void Configure(Uri server, string username, string password)
    {
        System.Diagnostics.Debug.WriteLine($"🌐 ApiContext.Configure called:");
        System.Diagnostics.Debug.WriteLine($"   Server: {server}");
        System.Diagnostics.Debug.WriteLine($"   Username: {username}");
        System.Diagnostics.Debug.WriteLine($"   Password length: {password?.Length ?? 0}");
        
        Server = server;
        Username = username;
        Password = password;
        Changed?.Invoke();
        
        System.Diagnostics.Debug.WriteLine($"🌐 ApiContext configured. IsConfigured: {IsConfigured}");
    }

    public void Clear()
    {
        System.Diagnostics.Debug.WriteLine($"🌐 ApiContext.Clear called");
        Server = null;
        Username = null;
        Password = null;
        Changed?.Invoke();
        System.Diagnostics.Debug.WriteLine($"🌐 ApiContext cleared. IsConfigured: {IsConfigured}");
    }

    public void NotifyAuthenticationExpired()
    {
        System.Diagnostics.Debug.WriteLine($"🚨 ApiContext: Authentication expired - notifying subscribers");
        AuthenticationExpired?.Invoke();
    }
}
