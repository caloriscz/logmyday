using CredentialManagement;
using LogMyDay.Installer.Models;

namespace LogMyDay.Installer.Services;

public class WindowsCredentialService : ICredentialService
{
    private const string CredentialPrefix = "LogMyDay:";

    public void SaveCredentials(string serverUrl, string username, string password)
    {
        try
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
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not save credentials to Windows Credential Manager: {ex.Message}");
            Console.WriteLine("Credentials will not be persisted for future use.");
        }
    }

    public ServerCredential? GetCredentials(string serverUrl)
    {
        try
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
        catch (Exception)
        {
            // CredentialManagement library may not be compatible with current .NET version
            // or Windows Credential Manager is not available
            return null;
        }
    }

    public void DeleteCredentials(string serverUrl)
    {
        try
        {
            var targetName = GetTargetName(serverUrl);
            
            using var credential = new Credential
            {
                Target = targetName,
                Type = CredentialType.Generic
            };
            
            credential.Delete();
        }
        catch (Exception)
        {
            // Silently fail if credential cannot be deleted
        }
    }

    public bool HasCredentials(string serverUrl)
    {
        try
        {
            var targetName = GetTargetName(serverUrl);
            
            using var credential = new Credential
            {
                Target = targetName,
                Type = CredentialType.Generic
            };
            
            return credential.Exists();
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string GetTargetName(string serverUrl)
    {
        return $"{CredentialPrefix}{serverUrl}";
    }
}
