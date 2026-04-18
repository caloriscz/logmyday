using System.Text.Json;

namespace LogMyDay.Cli.Services;

public class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".lmd", "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private record Config(string? ActiveAlias);

    public string? GetActiveAlias()
    {
        if (!File.Exists(ConfigPath))
        {
            return null;
        }

        var json = File.ReadAllText(ConfigPath);
        var config = JsonSerializer.Deserialize<Config>(json, JsonOpts);

        return config?.ActiveAlias;
    }

    public void SetActiveAlias(string? alias)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

        var config = new Config(alias);
        var json = JsonSerializer.Serialize(config, JsonOpts);

        File.WriteAllText(ConfigPath, json);
    }
}
