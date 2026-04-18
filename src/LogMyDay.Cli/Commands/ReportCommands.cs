using Cocona;
using LogMyDay.Cli.Formatting;
using LogMyDay.Cli.Services;
using LogMyDay.Shared.DTOs;
using System.Net.Http.Json;

namespace LogMyDay.Cli.Commands;

public class ReportCommands
{
    private readonly CliApiContext _apiContext;
    private readonly ApiClientFactory _apiClientFactory;
    private readonly ICredentialStore _credentialStore;
    private readonly ConfigManager _configManager;

    public ReportCommands(
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

    [Command("export", Description = "Download an Excel activity report")]
    public async Task Export(
        [Option("from", Description = "Start date (YYYY-MM-DD)")] string? from = null,
        [Option("to", Description = "End date (YYYY-MM-DD)")] string? to = null,
        [Option("preset", Description = "Date preset: last-month, last-quarter, last-year")] string? preset = null,
        [Option("tags", Description = "Comma-separated tag IDs to include (default: all tags)")] string? tags = null,
        [Option('o', Description = "Output file path (default: lmd-report-YYYY-MM-DD.xlsx in current directory)")] string? output = null)
    {
        LoadActiveAccount();

        DateTime? startDate;
        DateTime? endDate;

        if (preset is not null)
        {
            try
            {
                (startDate, endDate) = ResolvePreset(preset);
            }
            catch (ArgumentException ex)
            {
                OutputFormatter.WriteError(ex.Message);
                throw new CommandExitedException(1);
            }
        }
        else if (from is not null || to is not null)
        {
            startDate = from is not null ? DateTime.Parse(from) : null;
            endDate = to is not null ? DateTime.Parse(to) : null;
        }
        else
        {
            OutputFormatter.WriteError("Specify either --preset or --from/--to.");
            throw new CommandExitedException(1);
        }

        List<int> tagIds;

        if (tags is not null)
        {
            tagIds = tags.Split(',').Select(t => int.Parse(t.Trim())).ToList();
        }
        else
        {
            // API requires at least one tag — fetch all user's tags
            var activityApi = _apiClientFactory.CreateActivityApi();
            var allTags = await activityApi.GetTags();
            tagIds = allTags.Select(t => t.Id).ToList();

            if (tagIds.Count == 0)
            {
                OutputFormatter.WriteError("No tags found in the account. Create tags before exporting a report.");
                throw new CommandExitedException(1);
            }
        }

        var request = new ExcelExportRequest
        {
            TagIds = tagIds,
            StartDate = startDate,
            EndDate = endDate
        };

        var client = _apiClientFactory.CreateHttpClient();

        Console.Write("Generating report...");
        var response = await client.PostAsJsonAsync("/api/excelexport/generate", request);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine();
            string detail;
            try
            {
                var err = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                detail = err.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : await response.Content.ReadAsStringAsync();
            }
            catch
            {
                detail = await response.Content.ReadAsStringAsync();
            }
            OutputFormatter.WriteError($"Report generation failed ({(int)response.StatusCode}): {detail}");
            throw new CommandExitedException(1);
        }

        Console.WriteLine(" done.");

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? $"lmd-report-{DateTime.Today:yyyy-MM-dd}.xlsx";

        var outputPath = output ?? Path.Combine(Directory.GetCurrentDirectory(), fileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        await File.WriteAllBytesAsync(outputPath, bytes);

        OutputFormatter.WriteSuccess($"Report saved to: {outputPath}");
    }

    private static (DateTime? start, DateTime? end) ResolvePreset(string preset)
    {
        var today = DateTime.Today;

        return preset.ToLowerInvariant() switch
        {
            "last-month" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                             new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            "last-quarter" => ResolveLastQuarter(today),
            "last-year" => (new DateTime(today.Year - 1, 1, 1),
                            new DateTime(today.Year - 1, 12, 31)),
            _ => throw new ArgumentException($"Unknown preset '{preset}'. Valid values: last-month, last-quarter, last-year.")
        };
    }

    private static (DateTime start, DateTime end) ResolveLastQuarter(DateTime today)
    {
        var currentQuarter = (today.Month - 1) / 3 + 1;
        var lastQuarter = currentQuarter == 1 ? 4 : currentQuarter - 1;
        var year = currentQuarter == 1 ? today.Year - 1 : today.Year;
        var startMonth = (lastQuarter - 1) * 3 + 1;

        return (new DateTime(year, startMonth, 1),
                new DateTime(year, startMonth, 1).AddMonths(3).AddDays(-1));
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
