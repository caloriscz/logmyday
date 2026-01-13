using LogMyDay.Installer.Models;

namespace LogMyDay.Installer.Services;

public interface ICredentialService
{
    void SaveCredentials(string serverUrl, string username, string password);
    ServerCredential? GetCredentials(string serverUrl);
    void DeleteCredentials(string serverUrl);
    bool HasCredentials(string serverUrl);
}
