using Cocona;
using LogMyDay.Cli.Formatting;
using LogMyDay.Cli.Services;

namespace LogMyDay.Cli.Commands;

public class TagCommands
{
    private readonly CliApiContext _apiContext;
    private readonly ApiClientFactory _apiClientFactory;
    private readonly ICredentialStore _credentialStore;
    private readonly ConfigManager _configManager;
    private readonly TagResolver _tagResolver;

    public TagCommands(
        CliApiContext apiContext,
        ApiClientFactory apiClientFactory,
        ICredentialStore credentialStore,
        ConfigManager configManager,
        TagResolver tagResolver)
    {
        _apiContext = apiContext;
        _apiClientFactory = apiClientFactory;
        _credentialStore = credentialStore;
        _configManager = configManager;
        _tagResolver = tagResolver;
    }

    [Command("list", Description = "List all tags")]
    public async Task List(
        [Option("group", Description = "Filter by group name")] string? group = null,
        [Option("search", Description = "Filter by tag name")] string? search = null,
        [Option("json", Description = "Output as JSON")] bool json = false)
    {
        LoadActiveAccount();

        var api = _apiClientFactory.CreateActivityApi();
        var tags = await api.GetTags();

        var filtered = tags.AsEnumerable();

        if (group is not null)
        {
            filtered = filtered.Where(t =>
                t.GroupName is not null &&
                t.GroupName.Contains(group, StringComparison.OrdinalIgnoreCase));
        }

        if (search is not null)
        {
            filtered = filtered.Where(t =>
                t.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        OutputFormatter.WriteTags(filtered.ToList().AsReadOnly(), json);
    }

    [Command("show", Description = "Show details of a tag by name or ID")]
    public async Task Show(
        [Argument(Description = "Tag name or ID")] string nameOrId,
        [Option("json", Description = "Output as JSON")] bool json = false)
    {
        LoadActiveAccount();

        var api = _apiClientFactory.CreateActivityApi();
        var tag = await _tagResolver.ResolveAsync(api, nameOrId);

        if (tag is null)
        {
            OutputFormatter.WriteError($"Tag not found: {nameOrId}");
            throw new CommandExitedException(1);
        }

        OutputFormatter.WriteTagDetail(tag, json);
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
