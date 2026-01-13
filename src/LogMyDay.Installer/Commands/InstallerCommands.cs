using Cocona;
using LogMyDay.Installer.Models;
using LogMyDay.Installer.Services;
using LogMyDay.Shared;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Refit;
using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.Installer.Commands;

public class InstallerCommands
{
    private readonly ICredentialService _credentialService;
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configurationService;
    private readonly IServiceManagerService _serviceManager;
    private readonly IPrerequisiteChecker _prerequisiteChecker;
    private readonly IInstallationService _installationService;
    private readonly IHttpClientFactory _httpClientFactory;

    public InstallerCommands(
        ICredentialService credentialService,
        IGitHubService gitHubService,
        IConfigurationService configurationService,
        IServiceManagerService serviceManager,
        IPrerequisiteChecker prerequisiteChecker,
        IInstallationService installationService,
        IHttpClientFactory httpClientFactory)
    {
        _credentialService = credentialService;
        _gitHubService = gitHubService;
        _configurationService = configurationService;
        _serviceManager = serviceManager;
        _prerequisiteChecker = prerequisiteChecker;
        _installationService = installationService;
        _httpClientFactory = httpClientFactory;
    }

    [Command("install", Description = "Install LogMyDay server")]
    public async Task<int> InstallAsync(
        [Option('p', Description = "Installation path")] string? installPath = null,
        [Option('d', Description = "Database provider (SqlServer or SQLite)")] string? dbProvider = null,
        [Option('c', Description = "Database connection string")] string? connectionString = null,
        [Option('a', Description = "API base address")] string? apiAddress = null)
    {
        Console.WriteLine("=== LogMyDay Installer ===\n");

        // Check prerequisites
        Console.WriteLine("Checking prerequisites...");
        var prereqResult = await _prerequisiteChecker.CheckAllAsync();
        
        foreach (var message in prereqResult.Messages)
        {
            Console.WriteLine(message);
        }
        
        foreach (var warning in prereqResult.Warnings)
        {
            Console.WriteLine($"⚠ {warning}");
        }
        
        foreach (var error in prereqResult.Errors)
        {
            Console.WriteLine($"✗ {error}");
        }

        if (!prereqResult.IsSuccess)
        {
            Console.WriteLine("\nPrerequisite checks failed. Please install missing components and try again.");
            return 1;
        }

        Console.WriteLine();

        // Build installation configuration
        var config = new InstallationConfig
        {
            InstallPath = installPath ?? @"C:\Program Files\LogMyDay",
            ApiBaseAddress = apiAddress ?? "https://localhost:7064"
        };

        // Get database provider
        if (string.IsNullOrEmpty(dbProvider))
        {
            Console.WriteLine("Select database provider:");
            Console.WriteLine("1. SQL Server");
            Console.WriteLine("2. SQLite");
            Console.Write("Choice [1]: ");
            var choice = Console.ReadLine();
            config.DatabaseProvider = choice == "2" ? DatabaseProvider.SQLite : DatabaseProvider.SqlServer;
        }
        else
        {
            config.DatabaseProvider = dbProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase) 
                ? DatabaseProvider.SQLite 
                : DatabaseProvider.SqlServer;
        }

        // Get connection string
        if (string.IsNullOrEmpty(connectionString))
        {
            if (config.DatabaseProvider == DatabaseProvider.SQLite)
            {
                Console.Write($"SQLite database path [{Path.Combine(config.InstallPath, "logmyday.db")}]: ");
                var dbPath = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(dbPath))
                {
                    dbPath = Path.Combine(config.InstallPath, "logmyday.db");
                }
                config.ConnectionString = $"Data Source={dbPath}";
            }
            else
            {
                Console.Write("SQL Server connection string: ");
                config.ConnectionString = Console.ReadLine() ?? string.Empty;
            }
        }
        else
        {
            config.ConnectionString = connectionString;
        }

        // Optional: Email configuration
        Console.Write("\nConfigure email settings? [y/N]: ");
        if (Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true)
        {
            config.Email = new EmailConfiguration();
            Console.Write("SMTP Server: ");
            config.Email.SmtpServer = Console.ReadLine() ?? string.Empty;
            Console.Write("SMTP Port [587]: ");
            var portStr = Console.ReadLine();
            config.Email.SmtpPort = int.TryParse(portStr, out var port) ? port : 587;
            Console.Write("Use SSL? [Y/n]: ");
            config.Email.UseSsl = !Console.ReadLine()?.Trim().Equals("n", StringComparison.OrdinalIgnoreCase) == true;
            Console.Write("SMTP Username: ");
            config.Email.UserName = Console.ReadLine() ?? string.Empty;
            Console.Write("SMTP Password: ");
            config.Email.Password = ReadPassword();
            Console.Write("Sender Email: ");
            config.Email.SenderEmail = Console.ReadLine() ?? string.Empty;
        }

        Console.WriteLine();

        // Perform installation
        var success = await _installationService.PerformInstallationAsync(config);

        return success ? 0 : 1;
    }

    [Command("configure", Description = "Modify LogMyDay configuration")]
    public async Task<int> ConfigureAsync(
        [Option('p', Description = "Installation path")] string? installPath = null,
        [Option('s', Description = "Service name")] string serviceName = "LogMyDayApp")
    {
        installPath ??= @"C:\Program Files\LogMyDay";
        var configPath = Path.Combine(installPath, "appsettings.json");

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"✗ Configuration file not found: {configPath}");
            return 1;
        }

        Console.WriteLine("=== LogMyDay Configuration ===\n");
        Console.WriteLine("Loading current configuration...\n");

        var config = await _configurationService.ReadConfigurationAsync(configPath);

        Console.WriteLine("What would you like to configure?");
        Console.WriteLine("1. Database connection");
        Console.WriteLine("2. API base address");
        Console.WriteLine("3. Email settings");
        Console.Write("\nChoice: ");
        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                Console.Write($"Current connection: {config.ConnectionString}\n");
                Console.Write("New connection string: ");
                var newConn = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(newConn))
                {
                    config.ConnectionString = newConn;
                }
                break;

            case "2":
                Console.Write($"Current API address: {config.ApiBaseAddress}\n");
                Console.Write("New API address: ");
                var newApi = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(newApi))
                {
                    config.ApiBaseAddress = newApi;
                }
                break;

            case "3":
                config.Email ??= new EmailConfiguration();
                Console.Write($"SMTP Server [{config.Email.SmtpServer}]: ");
                var smtp = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(smtp)) config.Email.SmtpServer = smtp;
                
                Console.Write($"SMTP Port [{config.Email.SmtpPort}]: ");
                var portStr = Console.ReadLine();
                if (int.TryParse(portStr, out var port)) config.Email.SmtpPort = port;
                break;

            default:
                Console.WriteLine("Invalid choice");
                return 1;
        }

        // Save configuration
        config.InstallPath = installPath;
        await _configurationService.GenerateConfigurationAsync(config);

        Console.WriteLine("\n✓ Configuration updated");

        // Restart service
        Console.Write("Restart service now? [Y/n]: ");
        if (!Console.ReadLine()?.Trim().Equals("n", StringComparison.OrdinalIgnoreCase) == true)
        {
            Console.WriteLine("Restarting service...");
            await _serviceManager.RestartServiceAsync(serviceName);
            Console.WriteLine("✓ Service restarted");
        }

        return 0;
    }

    [Command("backup", Description = "Backup user data")]
    public async Task<int> BackupAsync(
        [Option('s', Description = "Server URL")] string serverUrl = "https://localhost:7064",
        [Option('o', Description = "Output file path")] string? outputPath = null,
        [Option('u', Description = "Username")] string? username = null,
        [Option('p', Description = "Password")] string? password = null)
    {
        Console.WriteLine("=== LogMyDay Backup ===\n");

        // Get credentials
        ServerCredential? credential = null;
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            credential = new ServerCredential
            {
                ServerUrl = serverUrl,
                Username = username,
                Password = password
            };
        }
        else
        {
            credential = _credentialService.GetCredentials(serverUrl);
            if (credential == null)
            {
                Console.Write("Username: ");
                var user = Console.ReadLine();
                Console.Write("Password: ");
                var pass = ReadPassword();

                if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                {
                    Console.WriteLine("✗ Credentials required");
                    return 1;
                }

                credential = new ServerCredential
                {
                    ServerUrl = serverUrl,
                    Username = user,
                    Password = pass
                };

                Console.Write("\nSave credentials? [Y/n]: ");
                if (!Console.ReadLine()?.Trim().Equals("n", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _credentialService.SaveCredentials(serverUrl, user, pass);
                    Console.WriteLine("✓ Credentials saved");
                }
            }
        }

        try
        {
            // Create API client
            var httpClient = _httpClientFactory.CreateClient("LogMyDayApi");
            httpClient.BaseAddress = new Uri(serverUrl);
            var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{credential.Username}:{credential.Password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var backupApi = RestService.For<ISecureBackupApi>(httpClient);

            Console.WriteLine("Creating backup...");
            var backup = await backupApi.CreateSecureBackupAsync();

            // Save to file
            outputPath ??= $"logmyday-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            var json = System.Text.Json.JsonSerializer.Serialize(backup, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(outputPath, json);

            Console.WriteLine($"✓ Backup saved to {outputPath}");
            Console.WriteLine($"  Created: {backup.CreatedAt}");
            Console.WriteLine($"  Activities: {backup.Activities?.Count ?? 0}");
            Console.WriteLine($"  Tags: {backup.Tags?.Count ?? 0}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Backup failed: {ex.Message}");
            return 1;
        }
    }

    [Command("restore", Description = "Restore user data from backup")]
    public async Task<int> RestoreAsync(
        [Argument(Description = "Backup file path")] string backupFile,
        [Option('s', Description = "Server URL")] string serverUrl = "https://localhost:7064",
        [Option('c', Description = "Clear existing data before restore")] bool clearExisting = false,
        [Option('u', Description = "Username")] string? username = null,
        [Option('p', Description = "Password")] string? password = null)
    {
        Console.WriteLine("=== LogMyDay Restore ===\n");

        if (!File.Exists(backupFile))
        {
            Console.WriteLine($"✗ Backup file not found: {backupFile}");
            return 1;
        }

        // Get credentials
        ServerCredential? credential = null;
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            credential = new ServerCredential
            {
                ServerUrl = serverUrl,
                Username = username,
                Password = password
            };
        }
        else
        {
            credential = _credentialService.GetCredentials(serverUrl);
            if (credential == null)
            {
                Console.Write("Username: ");
                var user = Console.ReadLine();
                Console.Write("Password: ");
                var pass = ReadPassword();

                if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                {
                    Console.WriteLine("✗ Credentials required");
                    return 1;
                }

                credential = new ServerCredential
                {
                    ServerUrl = serverUrl,
                    Username = user,
                    Password = pass
                };
            }
        }

        try
        {
            // Read backup file
            var json = await File.ReadAllTextAsync(backupFile);
            var backup = System.Text.Json.JsonSerializer.Deserialize<SecureBackupDto>(json);

            if (backup == null)
            {
                Console.WriteLine("✗ Invalid backup file");
                return 1;
            }

            // Create API client
            var httpClient = _httpClientFactory.CreateClient("LogMyDayApi");
            httpClient.BaseAddress = new Uri(serverUrl);
            var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{credential.Username}:{credential.Password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var backupApi = RestService.For<ISecureBackupApi>(httpClient);

            if (clearExisting)
            {
                Console.Write("⚠ This will delete all existing data. Continue? [y/N]: ");
                if (!Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Console.WriteLine("Restore cancelled");
                    return 0;
                }

                Console.WriteLine("Clearing existing data...");
                await backupApi.ClearCurrentUserDataAsync();
            }

            Console.WriteLine("Restoring backup...");
            var result = await backupApi.RestoreSecureBackupAsync(backup);

            Console.WriteLine($"✓ Restore completed");
            Console.WriteLine($"  Success: {result.Success}");
            Console.WriteLine($"  Message: {result.Message}");

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Restore failed: {ex.Message}");
            return 1;
        }
    }

    [Command("update", Description = "Update LogMyDay to the latest version")]
    public async Task<int> UpdateAsync(
        [Option('p', Description = "Installation path")] string? installPath = null,
        [Option('s', Description = "Service name")] string serviceName = "LogMyDayApp")
    {
        Console.WriteLine("=== LogMyDay Update ===\n");

        installPath ??= @"C:\Program Files\LogMyDay";

        if (!Directory.Exists(installPath))
        {
            Console.WriteLine($"✗ Installation not found at {installPath}");
            return 1;
        }

        try
        {
            // Check for updates
            Console.WriteLine("Checking for updates...");
            var latestVersion = await _gitHubService.GetLatestVersionAsync();
            Console.WriteLine($"Latest version: {latestVersion}");

            // TODO: Compare with current version
            Console.Write("Update to latest version? [Y/n]: ");
            if (Console.ReadLine()?.Trim().Equals("n", StringComparison.OrdinalIgnoreCase) == true)
            {
                return 0;
            }

            // Create backup
            Console.WriteLine("Creating backup of current installation...");
            var backupPath = $"{installPath}.backup.{DateTime.Now:yyyyMMdd-HHmmss}";
            CopyDirectory(installPath, backupPath);
            Console.WriteLine($"✓ Backup created at {backupPath}");

            // Stop service
            Console.WriteLine("Stopping service...");
            await _serviceManager.StopServiceAsync(serviceName);

            // Download and extract new version
            Console.WriteLine("Downloading new version...");
            var tempPath = Path.Combine(Path.GetTempPath(), "logmyday-update");
            var extractedPath = await _gitHubService.DownloadLatestReleaseAsync(tempPath);

            // Replace files (preserve configuration)
            Console.WriteLine("Updating files...");
            var configPath = Path.Combine(installPath, "appsettings.json");
            var configBackup = File.ReadAllText(configPath);

            CopyDirectory(extractedPath, installPath);

            // Restore configuration
            await File.WriteAllTextAsync(configPath, configBackup);

            // Start service
            Console.WriteLine("Starting service...");
            await _serviceManager.StartServiceAsync(serviceName);

            // Cleanup
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }

            Console.WriteLine($"\n✓ Update completed successfully!");
            Console.WriteLine($"  Version: {latestVersion}");
            Console.WriteLine($"  Backup: {backupPath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Update failed: {ex.Message}");
            return 1;
        }
    }

    [Command("status", Description = "Check LogMyDay service status")]
    public async Task<int> StatusAsync(
        [Option('s', Description = "Service name")] string serviceName = "LogMyDayApp")
    {
        Console.WriteLine("=== LogMyDay Status ===\n");

        var isInstalled = await _serviceManager.IsServiceInstalledAsync(serviceName);
        if (!isInstalled)
        {
            Console.WriteLine($"✗ Service '{serviceName}' is not installed");
            return 1;
        }

        Console.WriteLine($"✓ Service '{serviceName}' is installed");

        var isRunning = await _serviceManager.IsServiceRunningAsync(serviceName);
        Console.WriteLine($"  Status: {(isRunning ? "Running" : "Stopped")}");

        return 0;
    }

    private static string ReadPassword()
    {
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Backspace)
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
        }
        return password.ToString();
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            if (fileName == "appsettings.json" || fileName.StartsWith("appsettings."))
            {
                continue; // Skip configuration files during copy
            }
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
