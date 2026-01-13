using System.Diagnostics;
using LogMyDay.Installer.Models;

namespace LogMyDay.Installer.Services;

public class PrerequisiteChecker : IPrerequisiteChecker
{
    public async Task<PrerequisiteCheckResult> CheckAllAsync()
    {
        var result = new PrerequisiteCheckResult { IsSuccess = true };

        // Check .NET SDK
        if (await CheckDotNetSdkAsync())
        {
            result.Messages.Add("✓ .NET 9.0 SDK is installed");
        }
        else
        {
            result.Errors.Add("✗ .NET 9.0 SDK is not installed");
            result.IsSuccess = false;
        }

        return result;
    }

    public async Task<bool> CheckDotNetSdkAsync(string requiredVersion = "9.0")
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output.StartsWith(requiredVersion);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckSqlServerConnectivityAsync(string connectionString)
    {
        // This would require Microsoft.Data.SqlClient package
        // For now, return true as a placeholder
        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> CheckSqlitePathAsync(string sqlitePath)
    {
        await Task.CompletedTask;
        var directory = Path.GetDirectoryName(sqlitePath);
        return !string.IsNullOrEmpty(directory) && Directory.Exists(directory);
    }
}
