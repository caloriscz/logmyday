using System.Diagnostics;

namespace LogMyDay.App.Authentication;

public class CredentialStore
{
    private (string Username, string Password)? _credentials;
    private readonly int _id;

    public CredentialStore()
    {
        _id = GetHashCode();
        Debug.WriteLine($"============ [CredentialStore] Constructed instance {_id}");
    }

    public void Set(string username, string password)
    {
        Debug.WriteLine($"============ [CredentialStore:{_id}] Set credentials for {username}");
        _credentials = (username, password);
    }

    public (string Username, string Password)? Get()
    {
        Debug.WriteLine($"============ [CredentialStore:{_id}] Get credentials -> {_credentials?.Username ?? "null"}");
        return _credentials;
    }

    public void Clear()
    {
        Debug.WriteLine($"============ [CredentialStore:{_id}] Clear credentials");
        _credentials = null;
    }
}