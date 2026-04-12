using Cocona;
using LogMyDay.Cli.Formatting;
using LogMyDay.Cli.Services;
using Refit;
using Spectre.Console;

namespace LogMyDay.Cli.Commands;

public class AccountCommands
{
    private readonly ICredentialStore _credentialStore;
    private readonly ConfigManager _configManager;
    private readonly CliApiContext _apiContext;
    private readonly ApiClientFactory _apiClientFactory;

    public AccountCommands(
        ICredentialStore credentialStore,
        ConfigManager configManager,
        CliApiContext apiContext,
        ApiClientFactory apiClientFactory)
    {
        _credentialStore = credentialStore;
        _configManager = configManager;
        _apiContext = apiContext;
        _apiClientFactory = apiClientFactory;
    }

    [Command("login", Description = "Authenticate against a LogMyDay server and save credentials")]
    public async Task Login(
        [Option('s', Description = "Server URL (e.g. https://myserver.example.com)")] string server,
        [Option('u', Description = "Username (email address)")] string username,
        [Option('a', Description = "Friendly alias for this account")] string alias)
    {
        if (!Uri.TryCreate(server, UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != "https" && serverUri.Scheme != "http"))
        {
            OutputFormatter.WriteError($"Invalid server URL: {server}");
            throw new CommandExitedException(1);
        }

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Password:").Secret());

        _apiContext.Configure(serverUri, username, password);

        AnsiConsole.MarkupLine($"Connecting to [cyan]{Markup.Escape(server)}[/] ...");

        var authApi = _apiClientFactory.CreateAuthApi();

        try
        {
            var user = await authApi.GetCurrentUserAsync();

            _credentialStore.Save(alias, serverUri, username, password);

            if (_configManager.GetActiveAlias() is null)
            {
                _configManager.SetActiveAlias(alias);
            }

            OutputFormatter.WriteSuccess(
                $"Logged in as [cyan]{Markup.Escape(user.Email)}[/] on [cyan]{Markup.Escape(server)}[/] (alias: [yellow]{Markup.Escape(alias)}[/])");
        }
        catch (ApiException ex) when ((int)ex.StatusCode == 401)
        {
            _apiContext.Clear();
            OutputFormatter.WriteError("Invalid credentials.");
            throw new CommandExitedException(1);
        }
    }

    [Command("logout", Description = "Remove stored credentials for an account")]
    public Task Logout(
        [Argument(Description = "Account alias (omit to use active account)")] string? alias = null)
    {
        var target = alias ?? _configManager.GetActiveAlias();

        if (target is null)
        {
            OutputFormatter.WriteError("No active account. Specify an alias.");
            throw new CommandExitedException(1);
        }

        _credentialStore.Delete(target);

        if (_configManager.GetActiveAlias() == target)
        {
            _configManager.SetActiveAlias(null);
        }

        OutputFormatter.WriteSuccess($"Logged out of [yellow]{Markup.Escape(target)}[/]");

        return Task.CompletedTask;
    }

    [Command("accounts", Description = "List all stored accounts")]
    public Task Accounts(
        [Option("json", Description = "Output as JSON")] bool json = false)
    {
        var accounts = _credentialStore.LoadAll();
        var activeAlias = _configManager.GetActiveAlias();

        OutputFormatter.WriteAccounts(accounts, activeAlias, json);

        return Task.CompletedTask;
    }

    [Command("use", Description = "Switch the active account")]
    public Task Use(
        [Argument(Description = "Account alias")] string alias)
    {
        var cred = _credentialStore.Load(alias);

        if (cred is null)
        {
            OutputFormatter.WriteError(
                $"No account found with alias [yellow]{Markup.Escape(alias)}[/]. Run 'lmd accounts' to see available accounts.");
            throw new CommandExitedException(1);
        }

        _configManager.SetActiveAlias(alias);

        OutputFormatter.WriteSuccess(
            $"Active account set to [yellow]{Markup.Escape(alias)}[/] ({Markup.Escape(cred.Server.ToString())})");

        return Task.CompletedTask;
    }

    [Command("whoami", Description = "Show active account details and verify server connection")]
    public async Task Whoami(
        [Option("json", Description = "Output as JSON")] bool json = false)
    {
        var activeAlias = _configManager.GetActiveAlias();

        if (activeAlias is null)
        {
            OutputFormatter.WriteError("No active account. Run 'lmd login' first.");
            throw new CommandExitedException(1);
        }

        var cred = _credentialStore.Load(activeAlias);

        if (cred is null)
        {
            OutputFormatter.WriteError(
                $"No credentials found for alias [yellow]{Markup.Escape(activeAlias)}[/]. Run 'lmd login' to re-authenticate.");
            throw new CommandExitedException(1);
        }

        _apiContext.Configure(cred.Server, cred.Username, cred.Password);

        try
        {
            var authApi = _apiClientFactory.CreateAuthApi();
            var user = await authApi.GetCurrentUserAsync();

            OutputFormatter.WriteWhoami(activeAlias, cred, user.Email, user.DisplayName, user.IsAdmin, json);
        }
        catch (ApiException ex) when ((int)ex.StatusCode == 401)
        {
            _apiContext.Clear();
            OutputFormatter.WriteError(
                $"Authentication failed for alias [yellow]{Markup.Escape(activeAlias)}[/]. Run 'lmd login' to re-authenticate.");
            throw new CommandExitedException(1);
        }
    }
}
