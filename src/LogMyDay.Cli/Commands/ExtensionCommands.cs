using Cocona;
using LogMyDay.Cli.Formatting;
using LogMyDay.Cli.Services;
using Spectre.Console;
using System.Text.Json;

namespace LogMyDay.Cli.Commands;

public class ExtensionCommands
{
    private readonly CliApiContext _apiContext;
    private readonly ICredentialStore _credentialStore;
    private readonly ConfigManager _configManager;
    private readonly ExtensionManager _extensionManager;

    public ExtensionCommands(
        CliApiContext apiContext,
        ICredentialStore credentialStore,
        ConfigManager configManager,
        ExtensionManager extensionManager)
    {
        _apiContext = apiContext;
        _credentialStore = credentialStore;
        _configManager = configManager;
        _extensionManager = extensionManager;
    }

    [Command("list", Description = "List installed extensions")]
    public void List([Option("json", Description = "Output as JSON")] bool json = false)
    {
        var extensions = _extensionManager.GetAll();

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(extensions,
                new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        if (extensions.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No extensions installed.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Name");
        table.AddColumn("Version");
        table.AddColumn("Platforms");
        table.AddColumn("Description");

        foreach (var ext in extensions)
        {
            var platforms = ext.Platforms is { Length: > 0 }
                ? string.Join(", ", ext.Platforms)
                : "all";

            table.AddRow(
                Markup.Escape(ext.Name),
                Markup.Escape(ext.Version),
                Markup.Escape(platforms),
                Markup.Escape(ext.Description ?? ""));
        }

        AnsiConsole.Write(table);
    }

    [Command("install", Description = "Install an extension from a manifest file or directory")]
    public async Task Install(
        [Argument(Description = "Path to extension.json or directory containing one")] string path)
    {
        string manifestPath;

        if (Directory.Exists(path))
        {
            manifestPath = Path.Combine(path, "extension.json");
        }
        else if (File.Exists(path))
        {
            manifestPath = path;
        }
        else
        {
            OutputFormatter.WriteError($"Path not found: {path}");
            throw new CommandExitedException(1);
        }

        if (!File.Exists(manifestPath))
        {
            OutputFormatter.WriteError("extension.json not found.");
            throw new CommandExitedException(1);
        }

        ExtensionManifest manifest;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            manifest = JsonSerializer.Deserialize<ExtensionManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Invalid manifest.");
        }
        catch (Exception ex)
        {
            OutputFormatter.WriteError($"Failed to parse manifest: {ex.Message}");
            throw new CommandExitedException(1);
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            OutputFormatter.WriteError("Manifest is missing required field: name");
            throw new CommandExitedException(1);
        }

        if (string.IsNullOrWhiteSpace(manifest.Command))
        {
            OutputFormatter.WriteError("Manifest is missing required field: command");
            throw new CommandExitedException(1);
        }

        _extensionManager.Install(manifest, sourceDir: Path.GetDirectoryName(manifestPath)!);

        OutputFormatter.WriteSuccess($"Extension '{manifest.Name}' v{manifest.Version} installed.");
    }

    [Command("remove", Description = "Remove an installed extension")]
    public void Remove(
        [Argument(Description = "Extension name")] string name,
        [Option("yes", Description = "Skip confirmation prompt")] bool yes = false)
    {
        var existing = _extensionManager.Get(name);

        if (existing is null)
        {
            OutputFormatter.WriteError($"Extension '{name}' is not installed.");
            throw new CommandExitedException(1);
        }

        if (!yes)
        {
            Console.Write($"Remove extension '{name}'? [y/N] ");
            var answer = Console.ReadLine();

            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Cancelled.");
                return;
            }
        }

        _extensionManager.Remove(name);

        OutputFormatter.WriteSuccess($"Extension '{name}' removed.");
    }

    [Command("run", Description = "Run an installed extension")]
    public async Task Run(
        [Argument(Description = "Extension name")] string name,
        [Argument(Description = "Extra arguments passed through to the extension")] string[] extraArgs = default!)
    {
        LoadActiveAccount();

        int exitCode;

        try
        {
            exitCode = await _extensionManager.RunAsync(name, _apiContext, _credentialStore, _configManager, extraArgs);
        }
        catch (InvalidOperationException ex)
        {
            OutputFormatter.WriteError(ex.Message);
            throw new CommandExitedException(1);
        }

        if (exitCode != 0)
        {
            OutputFormatter.WriteError($"Extension exited with code {exitCode}.");
            throw new CommandExitedException(exitCode);
        }
    }

    [Command("show", Description = "Show details of an installed extension")]
    public void Show(
        [Argument(Description = "Extension name")] string name,
        [Option("json", Description = "Output as JSON")] bool json = false)
    {
        var ext = _extensionManager.Get(name);

        if (ext is null)
        {
            OutputFormatter.WriteError($"Extension '{name}' is not installed.");
            throw new CommandExitedException(1);
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(ext,
                new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Field");
        table.AddColumn("Value");

        table.AddRow("Name", Markup.Escape(ext.Name));
        table.AddRow("Version", Markup.Escape(ext.Version));
        table.AddRow("Command", Markup.Escape(ext.Command));
        table.AddRow("Description", Markup.Escape(ext.Description ?? ""));
        table.AddRow("Author", Markup.Escape(ext.Author ?? ""));
        table.AddRow("Platforms", ext.Platforms is { Length: > 0 }
            ? Markup.Escape(string.Join(", ", ext.Platforms))
            : "all");

        if (ext.Args is { Length: > 0 })
        {
            table.AddRow("Args", Markup.Escape(string.Join(" ", ext.Args)));
        }

        AnsiConsole.Write(table);
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
            OutputFormatter.WriteError($"Credentials for '{alias}' not found. Run 'lmd login' again.");
            throw new CommandExitedException(1);
        }

        _apiContext.Configure(cred.Server, cred.Username, cred.Password);
    }
}
