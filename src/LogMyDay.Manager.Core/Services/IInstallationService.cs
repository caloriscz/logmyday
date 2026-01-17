using LogMyDay.Manager.Core.Models;

namespace LogMyDay.Manager.Core.Services;

public interface IInstallationService
{
    Task<bool> PerformInstallationAsync(InstallationConfig config);
    Task<bool> InitializeDatabaseAsync(string installPath, DatabaseProvider provider, string connectionString);
    Task CreateStartMenuShortcutAsync(string installPath, string serviceName);
}
