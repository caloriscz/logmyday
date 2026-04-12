using Cocona;
using LogMyDay.Cli.Formatting;
using LogMyDay.Cli.Services;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Cli.Commands;

public class ActivityCommands
{
    private readonly CliApiContext _apiContext;
    private readonly ApiClientFactory _apiClientFactory;
    private readonly ICredentialStore _credentialStore;
    private readonly ConfigManager _configManager;
    private readonly TagResolver _tagResolver;
    private readonly BatchImporter _batchImporter;

    public ActivityCommands(
        CliApiContext apiContext,
        ApiClientFactory apiClientFactory,
        ICredentialStore credentialStore,
        ConfigManager configManager,
        TagResolver tagResolver,
        BatchImporter batchImporter)
    {
        _apiContext = apiContext;
        _apiClientFactory = apiClientFactory;
        _credentialStore = credentialStore;
        _configManager = configManager;
        _tagResolver = tagResolver;
        _batchImporter = batchImporter;
    }

    [Command("list", Description = "List activities")]
    public async Task List(
        [Option("tag", Description = "Filter by tag name or ID")] string? tag = null,
        [Option("from", Description = "Start date (YYYY-MM-DD)")] string? from = null,
        [Option("to", Description = "End date (YYYY-MM-DD)")] string? to = null,
        [Option("search", Description = "Filter by description text")] string? search = null,
        [Option("page", Description = "Page number")] int page = 1,
        [Option("size", Description = "Page size")] int size = 50,
        [Option("json", Description = "Output as JSON")] bool json = false)
    {
        LoadActiveAccount();

        var api = _apiClientFactory.CreateActivityApi();

        int? tagId = null;

        if (tag is not null)
        {
            var resolved = await _tagResolver.ResolveAsync(api, tag);

            if (resolved is null)
            {
                OutputFormatter.WriteError($"Tag not found: {tag}");
                throw new CommandExitedException(1);
            }

            tagId = resolved.Id;
        }

        var startDate = from is not null ? DateTime.Parse(from) : (DateTime?)null;
        var endDate = to is not null ? DateTime.Parse(to) : (DateTime?)null;

        var result = await api.GetActivities(
            pageNumber: page,
            pageSize: size,
            tagId: tagId,
            startDate: startDate,
            endDate: endDate,
            descriptionFilter: search);

        OutputFormatter.WriteActivities(result.Items, json);

        if (!json)
        {
            var totalPages = result.PageSize > 0
                ? (int)Math.Ceiling((double)result.TotalCount / result.PageSize)
                : 1;
            Console.WriteLine();
            Console.WriteLine($"Page {result.PageNumber} of {totalPages} ({result.TotalCount} total)");
        }
    }

    [Command("show", Description = "Show a single activity by ID")]
    public async Task Show(
        [Argument(Description = "Activity ID")] int id,
        [Option("json", Description = "Output as JSON")] bool json = false)
    {
        LoadActiveAccount();

        var api = _apiClientFactory.CreateActivityApi();
        var activity = await api.GetCalendarById(id);

        OutputFormatter.WriteActivities([activity], json);
    }

    [Command("add", Description = "Add a new activity")]
    public async Task Add(
        [Option("tag", Description = "Tag name or ID")] string tag,
        [Option("date", Description = "Date (YYYY-MM-DD or YYYY-MM-DDTHH:MM:SS)")] string date,
        [Option("value", Description = "Numeric value or description for the activity")] string? value = null,
        [Option("description", Description = "Description (alias for --value)")] string? description = null,
        [Option("end", Description = "End date/time for range activities (YYYY-MM-DD or YYYY-MM-DDTHH:MM:SS)")] string? end = null,
        [Option("json", Description = "Output created activity as JSON")] bool json = false)
    {
        LoadActiveAccount();

        var api = _apiClientFactory.CreateActivityApi();
        var resolved = await _tagResolver.ResolveAsync(api, tag);

        if (resolved is null)
        {
            OutputFormatter.WriteError($"Tag not found: {tag}");
            throw new CommandExitedException(1);
        }

        var dateStarted = DateTime.Parse(date);
        var dateFinished = end is not null ? DateTime.Parse(end) : (DateTime?)null;
        var desc = value ?? description;

        var request = new ActivityRequest
        {
            PrimaryTagId = resolved.Id,
            DateStarted = dateStarted,
            DateFinished = dateFinished,
            Description = desc
        };

        var created = await api.CreateCalendarItem(request);

        OutputFormatter.WriteActivities([created], json);

        if (!json)
        {
            OutputFormatter.WriteSuccess($"Activity {created.Id} created.");
        }
    }

    [Command("edit", Description = "Edit an existing activity")]
    public async Task Edit(
        [Argument(Description = "Activity ID")] int id,
        [Option("tag", Description = "New tag name or ID")] string? tag = null,
        [Option("date", Description = "New date (YYYY-MM-DD or YYYY-MM-DDTHH:MM:SS)")] string? date = null,
        [Option("value", Description = "New value or description")] string? value = null,
        [Option("end", Description = "New end date/time")] string? end = null,
        [Option("json", Description = "Output updated activity as JSON")] bool json = false)
    {
        LoadActiveAccount();

        var api = _apiClientFactory.CreateActivityApi();

        // Load current state to apply partial updates
        var current = await api.GetCalendarById(id);

        int? resolvedTagId = current.PrimaryTagId;

        if (tag is not null)
        {
            var resolved = await _tagResolver.ResolveAsync(api, tag);

            if (resolved is null)
            {
                OutputFormatter.WriteError($"Tag not found: {tag}");
                throw new CommandExitedException(1);
            }

            resolvedTagId = resolved.Id;
        }

        var request = new ActivityRequest
        {
            PrimaryTagId = resolvedTagId,
            DateStarted = date is not null ? DateTime.Parse(date) : current.DateStarted,
            DateFinished = end is not null ? DateTime.Parse(end) : current.DateFinished,
            Description = value ?? current.Description
        };

        var updated = await api.UpdateCalendarItem(id, request);

        OutputFormatter.WriteActivities([updated], json);

        if (!json)
        {
            OutputFormatter.WriteSuccess($"Activity {id} updated.");
        }
    }

    [Command("delete", Description = "Delete an activity")]
    public async Task Delete(
        [Argument(Description = "Activity ID")] int id,
        [Option("yes", Description = "Skip confirmation prompt")] bool yes = false)
    {
        LoadActiveAccount();

        if (!yes)
        {
            Console.Write($"Delete activity {id}? [y/N] ");
            var answer = Console.ReadLine();

            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Cancelled.");
                return;
            }
        }

        var api = _apiClientFactory.CreateActivityApi();
        await api.Delete(id);

        OutputFormatter.WriteSuccess($"Activity {id} deleted.");
    }

    [Command("import", Description = "Batch import activities from a CSV or JSON file")]
    public async Task Import(
        [Argument(Description = "Path to CSV or JSON import file")] string file,
        [Option("dry-run", Description = "Validate without importing")] bool dryRun = false)
    {
        if (!File.Exists(file))
        {
            OutputFormatter.WriteError($"File not found: {file}");
            throw new CommandExitedException(1);
        }

        LoadActiveAccount();

        var api = _apiClientFactory.CreateActivityApi();
        var result = await _batchImporter.ImportAsync(api, file, dryRun);

        Console.WriteLine();
        Console.WriteLine(dryRun ? "Dry run result:" : "Import result:");
        Console.WriteLine($"  Processed:  {result.Processed}");
        Console.WriteLine($"  Imported:   {result.Imported}");
        Console.WriteLine($"  Skipped:    {result.Skipped}");
        Console.WriteLine($"  Errors:     {result.Errors.Count}");

        if (result.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Errors:");

            foreach (var err in result.Errors)
            {
                Console.WriteLine($"  Line {err.Line}: {err.Message}");
            }
        }

        if (!dryRun && result.Errors.Count == 0)
        {
            OutputFormatter.WriteSuccess("Import completed.");
        }
        else if (result.Errors.Count > 0)
        {
            throw new CommandExitedException(1);
        }
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
