using LogMyDay.Installer.Models;

namespace LogMyDay.Installer.Services;

public interface IPrerequisiteChecker
{
    Task<PrerequisiteCheckResult> CheckAllAsync();
    Task<bool> CheckDotNetSdkAsync(string requiredVersion = "9.0");
    Task<bool> CheckSqlServerConnectivityAsync(string connectionString);
    Task<bool> CheckSqlitePathAsync(string sqlitePath);
}
