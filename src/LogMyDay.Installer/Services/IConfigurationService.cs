using LogMyDay.Installer.Models;

namespace LogMyDay.Installer.Services;

public interface IConfigurationService
{
    Task GenerateConfigurationAsync(InstallationConfig config);
    Task<InstallationConfig> ReadConfigurationAsync(string configPath);
    Task UpdateConfigurationAsync(string configPath, Action<InstallationConfig> updateAction);
    Task<InstallerConfig> LoadInstallerConfigAsync();
    Task SaveInstallerConfigAsync(InstallerConfig config);
    string GetInstallerConfigPath();
}
