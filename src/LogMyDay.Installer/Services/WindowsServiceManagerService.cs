using System.Diagnostics;

namespace LogMyDay.Installer.Services;

public class WindowsServiceManagerService : IServiceManagerService
{
    public async Task<bool> CreateServiceAsync(string serviceName, string displayName, string binaryPath, string? description = null)
    {
        var arguments = $"create \"{serviceName}\" binPath= \"\"{binaryPath}\"\" DisplayName= \"{displayName}\" start= auto";
        var result = await RunScCommandAsync(arguments);
        
        if (result && !string.IsNullOrEmpty(description))
        {
            await RunScCommandAsync($"description \"{serviceName}\" \"{description}\"");
        }

        return result;
    }

    public async Task<bool> StartServiceAsync(string serviceName)
    {
        return await RunScCommandAsync($"start \"{serviceName}\"");
    }

    public async Task<bool> StopServiceAsync(string serviceName)
    {
        return await RunScCommandAsync($"stop \"{serviceName}\"");
    }

    public async Task<bool> RestartServiceAsync(string serviceName)
    {
        await StopServiceAsync(serviceName);
        await Task.Delay(2000); // Wait for service to stop
        return await StartServiceAsync(serviceName);
    }

    public async Task<bool> DeleteServiceAsync(string serviceName)
    {
        return await RunScCommandAsync($"delete \"{serviceName}\"");
    }

    public async Task<bool> IsServiceInstalledAsync(string serviceName)
    {
        var result = await RunScCommandAsync($"query \"{serviceName}\"", throwOnError: false);
        return result;
    }

    public async Task<bool> IsServiceRunningAsync(string serviceName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"query \"{serviceName}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

        return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> RunScCommandAsync(string arguments, bool throwOnError = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Verb = "runas" // Run as administrator
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                if (throwOnError)
                {
                    throw new Exception("Failed to start sc.exe");
                }
                return false;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && throwOnError)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"sc.exe failed: {error}");
            }

            return process.ExitCode == 0;
        }
        catch when (!throwOnError)
        {
            return false;
        }
    }
}
