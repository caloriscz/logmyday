using Cocona;
using LogMyDay.Manager.Core.Models;
using LogMyDay.Manager.Core.Services;
using LogMyDay.Shared;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Refit;
using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.Manager.Cli.Commands;

public class ManagerCommands
{
    private readonly ICredentialService _credentialService;
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configurationService;
    private readonly IServiceManagerService _serviceManager;
    private readonly IPrerequisiteChecker _prerequisiteChecker;
    private readonly IInstallationService _installationService;
    private readonly IHttpClientFactory _httpClientFactory;

    public ManagerCommands(
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

    [PrimaryCommand]
    public async Task<int> DefaultAsync()
    {
        // When run without arguments, start interactive install
        Console.WriteLine("=== LogMyDay Manager ===\n");
        Console.WriteLine("Starting interactive installation...\n");
        
        return await InstallAsync();
    }

    [Command("install", Description = "Install LogMyDay server from GitHub Release")]
    public async Task<int> InstallAsync(
        [Option('p', Description = "Installation path")] string? installPath = null,
        [Option('d', Description = "Database provider (SqlServer or SQLite)")] string? dbProvider = null,
        [Option('c', Description = "Database connection string")] string? connectionString = null,
        [Option('a', Description = "API base address")] string? apiAddress = null)
    {
        Console.WriteLine("=== LogMyDay Installation ===\n");

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

    [Command("update", Description = "Update LogMyDay to the latest version from GitHub Release")]
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

    [Command("backup", Description = "Export user data to JSON file")]
    public async Task<int> BackupAsync(
        [Option('s', Description = "Server URL (uses default if not specified)")] string? serverUrl = null,
        [Option('o', Description = "Output file path")] string? outputPath = null,
        [Option('u', Description = "Username")] string? username = null,
        [Option('p', Description = "Password")] string? password = null)
    {
        Console.WriteLine("=== LogMyDay Backup ===\n");

        // Server URL is required for backup
        if (string.IsNullOrEmpty(serverUrl))
        {
            Console.WriteLine("Error: Server URL is required");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  logmyday backup -s <server-url>");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  logmyday backup -s https://logmyday.tadata.cz");
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

    [Command("restore", Description = "Import user data from backup file")]
    public async Task<int> RestoreAsync(
        [Argument(Description = "Backup file path")] string backupFile,
        [Option('s', Description = "Server URL (uses default if not specified)")] string? serverUrl = null,
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

        // Server URL is required for restore
        if (string.IsNullOrEmpty(serverUrl))
        {
            Console.WriteLine("Error: Server URL is required");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  logmyday restore <backup-file> -s <server-url>");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  logmyday restore backup.json -s https://logmyday.tadata.cz");
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

    [Command("status", Description = "Check LogMyDay installation and service status")]
    public async Task<int> StatusAsync(
        [Option('s', Description = "Service name")] string serviceName = "LogMyDayApp")
    {
        Console.WriteLine("=== LogMyDay Status ===\n");

        // Show manager configuration
        var config = await _configurationService.LoadManagerConfigAsync();
        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Config File: {_configurationService.GetManagerConfigPath()}");
        Console.WriteLine($"  Servers: {config.Servers.Count} configured");
        Console.WriteLine();

        // Check local service installation
        Console.WriteLine("Local Service:");
        var isInstalled = await _serviceManager.IsServiceInstalledAsync(serviceName);
        if (!isInstalled)
        {
            Console.WriteLine($"  Service '{serviceName}' is not installed");
            Console.WriteLine("  (This is OK if using remote LogMyDay server)");
        }
        else
        {
            Console.WriteLine($"  Service '{serviceName}' is installed");
            var isRunning = await _serviceManager.IsServiceRunningAsync(serviceName);
            Console.WriteLine($"  Status: {(isRunning ? "Running" : "Stopped")}");
        }

        Console.WriteLine("\nConfigured servers:");
        
        if (config.Servers.Count == 0)
        {
            Console.WriteLine("  (none)");
            Console.WriteLine();
            Console.WriteLine("Add a server with: logmyday server add <url> <username>");
        }
        else
        {
            Console.WriteLine();
            foreach (var server in config.Servers)
            {
                var hasCredentials = _credentialService.HasCredentials(server.Url);
                Console.WriteLine($"  • {server.Url} (user: {server.Username})");
                if (hasCredentials)
                {
                    Console.WriteLine("    Credentials: ✓ Saved");
                }
                else
                {
                    Console.WriteLine("    Credentials: ✗ Not saved");
                }
            }
        }

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
