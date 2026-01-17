using LogMyDay.Manager.Core.Models;

namespace LogMyDay.Manager.Core.Services;

public interface ICredentialService
{
    void SaveCredentials(string serverUrl, string username, string password);
    ServerCredential? GetCredentials(string serverUrl);
    void DeleteCredentials(string serverUrl);
    bool HasCredentials(string serverUrl);
}
