using Cocona;
using LogMyDay.Manager.Core.Models;
using LogMyDay.Manager.Core.Services;

namespace LogMyDay.Manager.Cli.Commands;

public class ServerCommands
{
    private readonly IConfigurationService _configService;
    private readonly ICredentialService _credentialService;

    public ServerCommands(
        IConfigurationService configService,
        ICredentialService credentialService)
    {
        _configService = configService;
        _credentialService = credentialService;
    }

    [Command("add", Description = "Add a LogMyDay server")]
    public async Task AddAsync(
        [Argument(Description = "Server URL (e.g., https://logmyday.example.com or https://localhost:7064)")] string url,
        [Argument(Description = "Username")] string username)
    {
        // Normalize URL
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = $"https://{url}";
        }

        // Validate URL format
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Console.WriteLine("Error: Invalid URL format");
            return;
        }

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            Console.WriteLine("Warning: HTTP is not secure. HTTPS is strongly recommended.");
            Console.Write("Continue with HTTP? (y/n): ");
            var response = Console.ReadLine();
            
            if (response?.ToLower() != "y")
            {
                return;
            }
        }

        // Remove trailing slash for consistency
        url = url.TrimEnd('/');

        // Check if server already exists
        var config = await _configService.LoadManagerConfigAsync();
        if (config.Servers.Any(s => s.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Error: Server already configured");
            Console.WriteLine("Use 'logmyday server list' to see all servers");
            return;
        }

        // Prompt for password
        Console.Write("Password: ");
        var password = ReadPassword();
        Console.WriteLine();

        if (string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("Error: Password cannot be empty");
            return;
        }

        // Save credentials
        try
        {
            _credentialService.SaveCredentials(url, username, password);
            Console.WriteLine("✓ Credentials saved to Windows Credential Manager");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not save credentials: {ex.Message}");
            Console.WriteLine("You will need to enter credentials again later");
        }

        // Add to config
        config.Servers.Add(new ServerConfig
        {
            Url = url,
            Username = username,
            LastAccessedUtc = DateTime.UtcNow
        });

        await _configService.SaveManagerConfigAsync(config);

        Console.WriteLine();
        Console.WriteLine($"✓ Server added successfully: {url}");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine("  - Run 'logmyday status' to check installation status");
        Console.WriteLine("  - Run 'logmyday backup -s <url>' to backup your data");
    }

    [Command("list", Description = "List configured servers")]
    public async Task ListAsync()
    {
        var config = await _configService.LoadManagerConfigAsync();

        if (config.Servers.Count == 0)
        {
            Console.WriteLine("No servers configured yet.");
            Console.WriteLine();
            Console.WriteLine("Add a server with:");
            Console.WriteLine("  logmyday server add <url> <username>");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  logmyday server add https://localhost:7064 admin");
            Console.WriteLine("  logmyday server add https://logmyday.example.com myuser");
            return;
        }

        Console.WriteLine("Configured servers:");
        Console.WriteLine();

        foreach (var server in config.Servers.OrderBy(s => s.Url))
        {
            var hasCredentials = _credentialService.HasCredentials(server.Url);
            var credStatus = hasCredentials ? "✓" : "✗";
            
            Console.WriteLine($"  {credStatus} {server.Url}");
            Console.WriteLine($"    Username: {server.Username}");
            Console.WriteLine($"    Credentials: {(hasCredentials ? "Saved in Windows Credential Manager" : "Not saved")}");
            Console.WriteLine($"    Last accessed: {server.LastAccessedUtc:yyyy-MM-dd HH:mm} UTC");
            Console.WriteLine();
        }

        Console.WriteLine("Legend:");
        Console.WriteLine("  ✓ = Credentials available");
        Console.WriteLine("  ✗ = Credentials missing (you will be prompted)");
    }

    [Command("remove", Description = "Remove a server configuration")]
    public async Task RemoveAsync(
        [Argument(Description = "Server URL")] string url)
    {
        // Normalize URL for comparison
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = $"https://{url}";
        }
        url = url.TrimEnd('/');

        var config = await _configService.LoadManagerConfigAsync();
        var server = config.Servers.FirstOrDefault(s => 
            s.Url.Equals(url, StringComparison.OrdinalIgnoreCase));

        if (server == null)
        {
            Console.WriteLine($"Error: Server '{url}' not found");
            Console.WriteLine();
            Console.WriteLine("Use 'logmyday server list' to see all servers");
            return;
        }

        Console.Write($"Remove server '{url}'? (y/n): ");
        var response = Console.ReadLine();
        
        if (response?.ToLower() != "y")
        {
            Console.WriteLine("Cancelled");
            return;
        }

        // Remove credentials
        try
        {
            _credentialService.DeleteCredentials(url);
            Console.WriteLine("✓ Credentials removed from Windows Credential Manager");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not remove credentials: {ex.Message}");
        }

        // Remove from config
        config.Servers.Remove(server);
        await _configService.SaveManagerConfigAsync(config);

        Console.WriteLine($"✓ Server removed successfully: {url}");
    }

    [Command("test", Description = "Test connection to a server")]
    public async Task TestAsync(
        [Argument(Description = "Server URL")] string url)
    {
        // Normalize URL
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = $"https://{url}";
        }
        url = url.TrimEnd('/');

        Console.WriteLine($"Testing connection to {url}...");
        Console.WriteLine();
        Console.WriteLine("Note: Connection test not yet implemented");
        Console.WriteLine("This feature will be added in a future version");
        Console.WriteLine();
        Console.WriteLine("For now, you can:");
        Console.WriteLine("  1. Open the URL in your browser");
        Console.WriteLine("  2. Check if LogMyDay is running");
        Console.WriteLine("  3. Verify you can log in");

        await Task.CompletedTask;
    }

    private static string ReadPassword()
    {
        var password = string.Empty;
        
        while (true)
        {
            var key = Console.ReadKey(true);
            
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }
            
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
                Console.Write("*");
            }
        }

        return password;
    }
}
