using LogMyDay.Manager.Core.Models;

namespace LogMyDay.Manager.Core.Services;

public interface IConfigurationService
{
    Task GenerateConfigurationAsync(InstallationConfig config);
    Task<InstallationConfig> ReadConfigurationAsync(string configPath);
    Task UpdateConfigurationAsync(string configPath, Action<InstallationConfig> updateAction);
    Task<ManagerConfig> LoadManagerConfigAsync();
    Task SaveManagerConfigAsync(ManagerConfig config);
    string GetManagerConfigPath();
}
