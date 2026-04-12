namespace LogMyDay.Cli.Services;

public class CliApiContext
{
    public Uri? Server { get; private set; }
    public string? Username { get; private set; }
    public string? Password { get; private set; }

    public bool IsConfigured => Server is not null && Username is not null && Password is not null;

    public void Configure(Uri server, string username, string password)
    {
        Server = server;
        Username = username;
        Password = password;
    }

    public void Clear()
    {
        Server = null;
        Username = null;
        Password = null;
    }
}
