using System.Text.Json;

namespace LogMyDay.Cli.Services;

public class ExtensionManager
{
    private readonly string _extensionsDir;

    public ExtensionManager()
    {
        _extensionsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".lmd", "extensions");
    }

    public List<ExtensionManifest> GetAll()
    {
        if (!Directory.Exists(_extensionsDir))
        {
            return [];
        }

        var manifests = new List<ExtensionManifest>();

        foreach (var dir in Directory.EnumerateDirectories(_extensionsDir))
        {
            var manifestPath = Path.Combine(dir, "extension.json");

            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = LoadManifest(manifestPath);
                manifests.Add(manifest);
            }
            catch
            {
                // Skip invalid manifests
            }
        }

        return manifests;
    }

    public ExtensionManifest? Get(string name)
    {
        var manifestPath = Path.Combine(_extensionsDir, name, "extension.json");

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        return LoadManifest(manifestPath);
    }

    public void Install(ExtensionManifest manifest, string? sourceDir = null)
    {
        var dir = Path.Combine(_extensionsDir, manifest.Name);

        // If reinstalling, remove existing files first
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);

        if (sourceDir is not null && Directory.Exists(sourceDir))
        {
            // Copy all files from the source directory
            foreach (var file in Directory.EnumerateFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), overwrite: true);
            }
        }
        else
        {
            // No source — just write the manifest
            var manifestPath = Path.Combine(dir, "extension.json");
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(manifestPath, json);
        }
    }

    public bool Remove(string name)
    {
        var dir = Path.Combine(_extensionsDir, name);

        if (!Directory.Exists(dir))
        {
            return false;
        }

        Directory.Delete(dir, recursive: true);

        return true;
    }

    public async Task<int> RunAsync(string name, CliApiContext apiContext, ICredentialStore credentialStore, ConfigManager configManager, string[]? extraArgs = null)
    {
        var manifest = Get(name);

        if (manifest is null)
        {
            throw new InvalidOperationException($"Extension '{name}' not found.");
        }

        if (!IsSupported(manifest))
        {
            throw new InvalidOperationException(
                $"Extension '{name}' does not support the current platform ({GetCurrentPlatform()}).");
        }

        var env = BuildEnvironment(apiContext, credentialStore, configManager);
        var extensionDir = Path.Combine(_extensionsDir, name);
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = manifest.Command,
            WorkingDirectory = extensionDir,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        if (manifest.Args is { Length: > 0 })
        {
            foreach (var arg in manifest.Args)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        if (extraArgs is { Length: > 0 })
        {
            foreach (var arg in extraArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        foreach (var kv in env)
        {
            startInfo.Environment[kv.Key] = kv.Value;
        }

        var process = System.Diagnostics.Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start extension '{name}'.");
        }

        await process.WaitForExitAsync();

        return process.ExitCode;
    }

    private static Dictionary<string, string> BuildEnvironment(
        CliApiContext apiContext,
        ICredentialStore credentialStore,
        ConfigManager configManager)
    {
        var env = new Dictionary<string, string>();
        var alias = configManager.GetActiveAlias();

        if (alias is not null)
        {
            env["LMD_ALIAS"] = alias;
        }

        if (apiContext.Server is not null)
        {
            env["LMD_SERVER"] = apiContext.Server.ToString().TrimEnd('/');
        }

        if (apiContext.Username is not null)
        {
            env["LMD_USERNAME"] = apiContext.Username;
        }

        if (apiContext.Username is not null && apiContext.Password is not null)
        {
            var token = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{apiContext.Username}:{apiContext.Password}"));
            env["LMD_TOKEN"] = token;
        }

        return env;
    }

    private bool IsSupported(ExtensionManifest manifest)
    {
        if (manifest.Platforms is null || manifest.Platforms.Length == 0)
        {
            return true;
        }

        var current = GetCurrentPlatform();

        return manifest.Platforms.Contains(current, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macos";
        if (OperatingSystem.IsLinux()) return "linux";

        return "unknown";
    }

    private static ExtensionManifest LoadManifest(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ExtensionManifest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid extension manifest.");
    }
}

public class ExtensionManifest
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string Command { get; set; } = "";
    public string[]? Args { get; set; }
    public string[]? Platforms { get; set; }
}
