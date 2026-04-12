using Spectre.Console;
using System.Text.Json;
using LogMyDay.Cli.Services;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Cli.Formatting;

public static class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void WriteAccounts(IReadOnlyList<StoredCredential> accounts, string? activeAlias, bool json)
    {
        if (json)
        {
            var output = accounts.Select(a => new
            {
                alias = a.Alias,
                server = a.Server.ToString(),
                username = a.Username,
                active = a.Alias == activeAlias
            });

            Console.WriteLine(JsonSerializer.Serialize(output, JsonOpts));
            return;
        }

        if (accounts.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No accounts stored. Run 'lmd login' to add one.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Alias");
        table.AddColumn("Server");
        table.AddColumn("Username");
        table.AddColumn("Active");

        foreach (var account in accounts)
        {
            table.AddRow(
                account.Alias,
                account.Server.ToString(),
                account.Username,
                account.Alias == activeAlias ? "[green]yes[/]" : "");
        }

        AnsiConsole.Write(table);
    }

    public static void WriteWhoami(string alias, StoredCredential cred, string? email, string? displayName, bool isAdmin, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                alias,
                server = cred.Server.ToString(),
                username = cred.Username,
                email,
                displayName,
                isAdmin
            }, JsonOpts));
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Field");
        table.AddColumn("Value");
        table.AddRow("Alias", $"[yellow]{alias}[/]");
        table.AddRow("Server", $"[cyan]{cred.Server}[/]");
        table.AddRow("Username", cred.Username);

        if (!string.IsNullOrEmpty(displayName))
        {
            table.AddRow("Display Name", displayName);
        }

        table.AddRow("Admin", isAdmin ? "[green]yes[/]" : "no");

        AnsiConsole.Write(table);
    }

    public static void WriteSuccess(string message) =>
        AnsiConsole.MarkupLine($"[green]OK[/] {message}");

    public static void WriteError(string message) =>
        AnsiConsole.MarkupLine($"[red]Error:[/] {message}");

    public static void WriteActivities(IReadOnlyList<ActivityResponse> activities, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(activities, JsonOpts));
            return;
        }

        if (activities.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No activities found.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Date");
        table.AddColumn("Tag");
        table.AddColumn("Value");
        table.AddColumn("Description");

        foreach (var a in activities)
        {
            table.AddRow(
                a.Id.ToString(),
                a.DateStarted.ToString("yyyy-MM-dd"),
                Markup.Escape(a.PrimaryTagName ?? ""),
                Markup.Escape(a.PrimaryTagValue ?? ""),
                Markup.Escape(a.Description ?? ""));
        }

        AnsiConsole.Write(table);
    }

    public static void WriteTags(IReadOnlyList<TagResponse> tags, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(tags, JsonOpts));
            return;
        }

        if (tags.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No tags found.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Type");
        table.AddColumn("Unit");
        table.AddColumn("Group");

        foreach (var t in tags)
        {
            table.AddRow(
                t.Id.ToString(),
                Markup.Escape(t.Title),
                t.InputTypeId?.ToString() ?? "",
                Markup.Escape(t.UnitSymbol ?? ""),
                Markup.Escape(t.GroupName ?? ""));
        }

        AnsiConsole.Write(table);
    }

    public static void WriteTagDetail(TagResponse t, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(t, JsonOpts));
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Field");
        table.AddColumn("Value");

        table.AddRow("ID", t.Id.ToString());
        table.AddRow("Name", Markup.Escape(t.Title));
        table.AddRow("Description", Markup.Escape(t.Description ?? ""));
        table.AddRow("Input Type ID", t.InputTypeId?.ToString() ?? "");
        table.AddRow("Unit", Markup.Escape(t.UnitSymbol ?? ""));
        table.AddRow("Group", Markup.Escape(t.GroupName ?? ""));
        table.AddRow("Required", t.IsRequired ? "yes" : "no");
        table.AddRow("Repeatable", t.IsRepeatable ? "yes" : "no");
        table.AddRow("Range", t.IsRange ? "yes" : "no");

        if (t.MinValue.HasValue) table.AddRow("Min Value", t.MinValue.Value.ToString());
        if (t.MaxValue.HasValue) table.AddRow("Max Value", t.MaxValue.Value.ToString());
        if (t.DefaultValue is not null) table.AddRow("Default", Markup.Escape(t.DefaultValue));
        if (t.OptionListName is not null) table.AddRow("Options", Markup.Escape(t.OptionListName));

        AnsiConsole.Write(table);
    }
}
