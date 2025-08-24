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
}

public class ApiContext : IApiContext
{
    public Uri? Server { get; private set; }
    public string? Username { get; private set; }
    public string? Password { get; private set; }

    public event Action? Changed;

    public bool IsConfigured => Server != null && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

    public void Configure(Uri server, string username, string password)
    {
        Server = server;
        Username = username;
        Password = password;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Server = null;
        Username = null;
        Password = null;
        Changed?.Invoke();
    }
}
