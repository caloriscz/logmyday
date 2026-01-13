using System.Text;
using System.Text.Json;
using LogMyDay.Installer.Models;

namespace LogMyDay.Installer.Services;

public class ConfigurationService : IConfigurationService
{
    public async Task GenerateConfigurationAsync(InstallationConfig config)
    {
        var configPath = Path.Combine(config.InstallPath, "appsettings.json");
        
        var appSettings = new
        {
            ConnectionStrings = new
            {
                DefaultConnection = config.ConnectionString,
                DatabaseProvider = config.DatabaseProvider.ToString()
            },
            Api = new
            {
                BaseAddress = config.ApiBaseAddress
            },
            Email = config.Email != null ? new
            {
                SmtpServer = config.Email.SmtpServer,
                SmtpPort = config.Email.SmtpPort,
                UseSsl = config.Email.UseSsl,
                UserName = config.Email.UserName,
                Password = config.Email.Password,
                SenderEmail = config.Email.SenderEmail,
                SenderName = config.Email.SenderName,
                PasswordResetUrl = $"{config.ApiBaseAddress}/reset-password"
            } : null,
            Logging = new
            {
                LogLevel = new
                {
                    Default = "Information",
                    Microsoft = "Warning",
                    MicrosoftAspNetCore = "Warning"
                }
            },
            AllowedHosts = "*"
        };

        var json = JsonSerializer.Serialize(appSettings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        Directory.CreateDirectory(config.InstallPath);
        await File.WriteAllTextAsync(configPath, json, Encoding.UTF8);
    }

    public async Task<InstallationConfig> ReadConfigurationAsync(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = await File.ReadAllTextAsync(configPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var config = new InstallationConfig();

        if (root.TryGetProperty("ConnectionStrings", out var connStrings))
        {
            if (connStrings.TryGetProperty("DefaultConnection", out var defaultConn))
            {
                config.ConnectionString = defaultConn.GetString() ?? string.Empty;
            }
            if (connStrings.TryGetProperty("DatabaseProvider", out var dbProvider))
            {
                var providerStr = dbProvider.GetString();
                config.DatabaseProvider = providerStr == "SQLite" 
                    ? DatabaseProvider.SQLite 
                    : DatabaseProvider.SqlServer;
            }
        }

        if (root.TryGetProperty("Api", out var api) && api.TryGetProperty("BaseAddress", out var baseAddr))
        {
            config.ApiBaseAddress = baseAddr.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("Email", out var email))
        {
            config.Email = new EmailConfiguration
            {
                SmtpServer = email.TryGetProperty("SmtpServer", out var smtp) ? smtp.GetString() ?? string.Empty : string.Empty,
                SmtpPort = email.TryGetProperty("SmtpPort", out var port) ? port.GetInt32() : 587,
                UseSsl = email.TryGetProperty("UseSsl", out var ssl) && ssl.GetBoolean(),
                UserName = email.TryGetProperty("UserName", out var user) ? user.GetString() ?? string.Empty : string.Empty,
                Password = email.TryGetProperty("Password", out var pass) ? pass.GetString() ?? string.Empty : string.Empty,
                SenderEmail = email.TryGetProperty("SenderEmail", out var sender) ? sender.GetString() ?? string.Empty : string.Empty,
                SenderName = email.TryGetProperty("SenderName", out var name) ? name.GetString() ?? "LogMyDay" : "LogMyDay"
            };
        }

        return config;
    }

    public async Task UpdateConfigurationAsync(string configPath, Action<InstallationConfig> updateAction)
    {
        var config = await ReadConfigurationAsync(configPath);
        updateAction(config);
        
        var installPath = Path.GetDirectoryName(configPath) ?? throw new InvalidOperationException("Invalid config path");
        config.InstallPath = installPath;
        
        await GenerateConfigurationAsync(config);
    }
}
