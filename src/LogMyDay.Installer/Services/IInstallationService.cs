using LogMyDay.Installer.Models;

namespace LogMyDay.Installer.Services;

public interface IInstallationService
{
    Task<bool> PerformInstallationAsync(InstallationConfig config);
    Task<bool> InitializeDatabaseAsync(string installPath, DatabaseProvider provider, string connectionString);
    Task CreateStartMenuShortcutAsync(string installPath, string serviceName);
}
