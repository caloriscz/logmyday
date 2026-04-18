namespace LogMyDay.Cli.Services;

public interface ICredentialStore
{
    void Save(string alias, Uri server, string username, string password);
    StoredCredential? Load(string alias);
    void Delete(string alias);
    IReadOnlyList<StoredCredential> LoadAll();
}

public record StoredCredential(string Alias, Uri Server, string Username, string Password);
