using CredentialManagement;
using LogMyDay.Installer.Models;

namespace LogMyDay.Installer.Services;

public class WindowsCredentialService : ICredentialService
{
    private const string CredentialPrefix = "LogMyDay:";

    public void SaveCredentials(string serverUrl, string username, string password)
    {
        var targetName = GetTargetName(serverUrl);
        
        using var credential = new Credential
        {
            Target = targetName,
            Username = username,
            Password = password,
            Type = CredentialType.Generic,
            PersistanceType = PersistanceType.LocalComputer
        };
        
        credential.Save();
    }

    public ServerCredential? GetCredentials(string serverUrl)
    {
        var targetName = GetTargetName(serverUrl);
        
        using var credential = new Credential
        {
            Target = targetName,
            Type = CredentialType.Generic
        };
        
        if (!credential.Load())
        {
            return null;
        }

        return new ServerCredential
        {
            ServerUrl = serverUrl,
            Username = credential.Username,
            Password = credential.Password
        };
    }

    public void DeleteCredentials(string serverUrl)
    {
        var targetName = GetTargetName(serverUrl);
        
        using var credential = new Credential
        {
            Target = targetName,
            Type = CredentialType.Generic
        };
        
        credential.Delete();
    }

    public bool HasCredentials(string serverUrl)
    {
        var targetName = GetTargetName(serverUrl);
        
        using var credential = new Credential
        {
            Target = targetName,
            Type = CredentialType.Generic
        };
        
        return credential.Exists();
    }

    private static string GetTargetName(string serverUrl)
    {
        return $"{CredentialPrefix}{serverUrl}";
    }
}
