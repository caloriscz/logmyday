namespace LogMyDay.App.Authentication;

public class CredentialStore
{
    private (string Username, string Password)? _credentials;
    private readonly int _id;

    public CredentialStore()
    {
        _id = GetHashCode();
    }

    public void Set(string username, string password)
    {
        _credentials = (username, password);
    }

    public (string Username, string Password)? Get()
    {
        return _credentials;
    }

    public void Clear()
    {
        _credentials = null;
    }
}