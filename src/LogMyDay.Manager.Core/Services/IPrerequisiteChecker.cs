using LogMyDay.Manager.Core.Models;

namespace LogMyDay.Manager.Core.Services;

public interface IPrerequisiteChecker
{
    Task<PrerequisiteCheckResult> CheckAllAsync();
    Task<bool> CheckDotNetSdkAsync(string requiredVersion = "9.0");
    Task<bool> CheckSqlServerConnectivityAsync(string connectionString);
    Task<bool> CheckSqlitePathAsync(string sqlitePath);
}
