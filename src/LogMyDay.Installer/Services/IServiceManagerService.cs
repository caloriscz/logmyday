namespace LogMyDay.Installer.Services;

public interface IServiceManagerService
{
    Task<bool> CreateServiceAsync(string serviceName, string displayName, string binaryPath, string? description = null);
    Task<bool> StartServiceAsync(string serviceName);
    Task<bool> StopServiceAsync(string serviceName);
    Task<bool> RestartServiceAsync(string serviceName);
    Task<bool> DeleteServiceAsync(string serviceName);
    Task<bool> IsServiceInstalledAsync(string serviceName);
    Task<bool> IsServiceRunningAsync(string serviceName);
}
