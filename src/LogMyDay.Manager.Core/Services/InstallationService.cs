using System.Diagnostics;
using LogMyDay.Manager.Core.Models;

namespace LogMyDay.Manager.Core.Services;

public class InstallationService : IInstallationService
{
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configurationService;
    private readonly IServiceManagerService _serviceManager;

    public InstallationService(
        IGitHubService gitHubService,
        IConfigurationService configurationService,
        IServiceManagerService serviceManager)
    {
        _gitHubService = gitHubService;
        _configurationService = configurationService;
        _serviceManager = serviceManager;
    }

    public async Task<bool> PerformInstallationAsync(InstallationConfig config)
    {
        try
        {
            Console.WriteLine("Starting LogMyDay installation...");

            // Download and extract binaries
            Console.WriteLine("Downloading latest release from GitHub...");
            var tempPath = Path.Combine(Path.GetTempPath(), "logmyday-install");
            var extractedPath = await _gitHubService.DownloadLatestReleaseAsync(tempPath);

            // Copy files to installation directory
            Console.WriteLine($"Installing to {config.InstallPath}...");
            Directory.CreateDirectory(config.InstallPath);
            CopyDirectory(extractedPath, config.InstallPath);

            // Generate configuration
            Console.WriteLine("Generating configuration files...");
            await _configurationService.GenerateConfigurationAsync(config);

            // Initialize database
            Console.WriteLine("Initializing database...");
            await InitializeDatabaseAsync(config.InstallPath, config.DatabaseProvider, config.ConnectionString);

            // Register Windows service
            Console.WriteLine("Registering Windows service...");
            var exePath = Path.Combine(config.InstallPath, "LogMyDay.App.exe");
            await _serviceManager.CreateServiceAsync(
                config.ServiceName,
                config.ServiceDisplayName,
                exePath,
                "LogMyDay personal activity logging application");

            // Start the service
            Console.WriteLine("Starting service...");
            await _serviceManager.StartServiceAsync(config.ServiceName);

            // Create Start Menu shortcut
            Console.WriteLine("Creating Start Menu shortcut...");
            await CreateStartMenuShortcutAsync(config.InstallPath, config.ServiceName);

            Console.WriteLine($"\n✓ Installation completed successfully!");
            Console.WriteLine($"  Service: {config.ServiceDisplayName}");
            Console.WriteLine($"  URL: {config.ApiBaseAddress}");

            // Cleanup temp files
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Installation failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> InitializeDatabaseAsync(string installPath, DatabaseProvider provider, string connectionString)
    {
        try
        {
            if (provider == DatabaseProvider.SQLite)
            {
                // For SQLite, just ensure the directory exists
                var dbPath = connectionString.Replace("Data Source=", "").Trim();
                var dbDir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dbDir))
                {
                    Directory.CreateDirectory(dbDir);
                }
            }

            // Run EF migrations
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "ef database update",
                WorkingDirectory = installPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new Exception("Failed to start dotnet ef");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Database initialization output: {output}");
                Console.WriteLine($"Database initialization error: {error}");
                throw new Exception($"Database initialization failed with exit code {process.ExitCode}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Database initialization failed: {ex.Message}");
            Console.WriteLine("You may need to run database migrations manually.");
            return false;
        }
    }

    public async Task CreateStartMenuShortcutAsync(string installPath, string serviceName)
    {
        await Task.CompletedTask;
        
        // For now, just create a simple text file with instructions
        // In a full implementation, this would create a proper Windows shortcut
        var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        var programsPath = Path.Combine(startMenuPath, "Programs", "LogMyDay");
        
        Directory.CreateDirectory(programsPath);
        
        var readmePath = Path.Combine(programsPath, "README.txt");
        await File.WriteAllTextAsync(readmePath, $@"LogMyDay has been installed as a Windows service.

Service Name: {serviceName}

To manage the service, use Windows Services (services.msc) or the logmyday command-line tool.

Installation Path: {installPath}
");
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(targetDir, fileName);
            File.Copy(file, destFile, true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(directory);
            var destDir = Path.Combine(targetDir, dirName);
            CopyDirectory(directory, destDir);
        }
    }
}
