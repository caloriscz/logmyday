using Cocona;
using LogMyDay.Cli.Formatting;
using LogMyDay.Cli.Services;
using LogMyDay.Shared.DTOs;
using System.Text.Json;

namespace LogMyDay.Cli.Commands;

public class BackupCommands
{
    private readonly CliApiContext _apiContext;
    private readonly ApiClientFactory _apiClientFactory;
    private readonly ICredentialStore _credentialStore;
    private readonly ConfigManager _configManager;

    public BackupCommands(
        CliApiContext apiContext,
        ApiClientFactory apiClientFactory,
        ICredentialStore credentialStore,
        ConfigManager configManager)
    {
        _apiContext = apiContext;
        _apiClientFactory = apiClientFactory;
        _credentialStore = credentialStore;
        _configManager = configManager;
    }

    [Command("export", Description = "Download a backup of all activities and tags")]
    public async Task Export(
        [Option('o', Description = "Output file path (default: lmd-backup-YYYY-MM-DD.json in current directory)")] string? output = null)
    {
        LoadActiveAccount();

        var backupApi = _apiClientFactory.CreateBackupApi();

        Console.Write("Downloading backup...");
        var backup = await backupApi.CreateSecureBackupAsync();
        Console.WriteLine(" done.");

        var outputPath = output ?? Path.Combine(
            Directory.GetCurrentDirectory(),
            $"lmd-backup-{DateTime.Today:yyyy-MM-dd}.json");

        var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(outputPath, json);

        OutputFormatter.WriteSuccess($"Backup saved to: {outputPath}");
    }

    [Command("import", Description = "Restore activities and tags from a backup file")]
    public async Task Import(
        [Argument(Description = "Path to backup JSON file")] string file)
    {
        if (!File.Exists(file))
        {
            OutputFormatter.WriteError($"File not found: {file}");
            throw new CommandExitedException(1);
        }

        LoadActiveAccount();

        var json = await File.ReadAllTextAsync(file);
        SecureBackupDto backup;

        try
        {
            backup = JsonSerializer.Deserialize<SecureBackupDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Backup file is empty or invalid.");
        }
        catch (Exception ex)
        {
            OutputFormatter.WriteError($"Failed to parse backup file: {ex.Message}");
            throw new CommandExitedException(1);
        }

        var backupApi = _apiClientFactory.CreateBackupApi();

        Console.Write("Restoring backup...");
        var result = await backupApi.RestoreSecureBackupAsync(backup);
        Console.WriteLine(" done.");

        if (!result.Success)
        {
            OutputFormatter.WriteError($"Import failed: {result.Message}");

            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error}");
            }

            throw new CommandExitedException(1);
        }

        var s = result.Statistics;
        Console.WriteLine();
        Console.WriteLine("Import statistics:");
        Console.WriteLine($"  Tags:         {s.TagsImported} imported, {s.TagsSkipped} skipped");
        Console.WriteLine($"  Activities:   {s.ActivitiesImported} imported, {s.ActivitiesSkipped} skipped");
        Console.WriteLine($"  Tag groups:   {s.TagGroupsImported} imported, {s.TagGroupsSkipped} skipped");
        Console.WriteLine($"  Units:        {s.UnitsImported} imported, {s.UnitsSkipped} skipped");

        if (result.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Warnings:");

            foreach (var warn in result.Warnings)
            {
                Console.WriteLine($"  - {warn}");
            }
        }

        OutputFormatter.WriteSuccess("Import complete.");
    }

    private void LoadActiveAccount()
    {
        var alias = _configManager.GetActiveAlias();

        if (alias is null)
        {
            OutputFormatter.WriteError("No active account. Run 'lmd login' first.");
            throw new CommandExitedException(1);
        }

        var cred = _credentialStore.Load(alias);

        if (cred is null)
        {
            OutputFormatter.WriteError($"Credentials not found for alias '{alias}'. Run 'lmd login' to re-authenticate.");
            throw new CommandExitedException(1);
        }

        _apiContext.Configure(cred.Server, cred.Username, cred.Password);
    }
}
