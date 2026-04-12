using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace LogMyDay.Cli.Services;

public class BatchImporter
{
    private readonly TagResolver _tagResolver;

    public BatchImporter(TagResolver tagResolver)
    {
        _tagResolver = tagResolver;
    }

    public async Task<BatchImportResult> ImportAsync(IActivityApi api, string filePath, bool dryRun)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        var rows = ext switch
        {
            ".csv" => await ParseCsvAsync(filePath),
            ".json" => await ParseJsonAsync(filePath),
            _ => throw new InvalidOperationException($"Unsupported file format '{ext}'. Use .csv or .json.")
        };

        var result = new BatchImportResult { Processed = rows.Count };

        foreach (var (row, index) in rows.Select((r, i) => (r, i + 1)))
        {
            var tag = await _tagResolver.ResolveAsync(api, row.Tag);

            if (tag is null)
            {
                result.Errors.Add(new BatchImportError
                {
                    Line = index,
                    Message = $"Tag not found: '{row.Tag}'"
                });
                result.Skipped++;
                continue;
            }

            if (!DateTime.TryParse(row.Date, out var dateStarted))
            {
                result.Errors.Add(new BatchImportError
                {
                    Line = index,
                    Message = $"Invalid date: '{row.Date}'"
                });
                result.Skipped++;
                continue;
            }

            if (dryRun)
            {
                result.Imported++;
                continue;
            }

            try
            {
                await api.CreateCalendarItem(new ActivityRequest
                {
                    PrimaryTagId = tag.Id,
                    DateStarted = dateStarted,
                    Description = row.Value ?? row.Description
                });

                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new BatchImportError
                {
                    Line = index,
                    Message = ex.Message
                });
                result.Skipped++;
            }
        }

        return result;
    }

    private static async Task<List<BatchRow>> ParseCsvAsync(string filePath)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        var rows = new List<BatchRow>();

        if (lines.Length == 0)
        {
            return rows;
        }

        // Detect header: first non-empty line
        var headerLine = lines[0].Trim();
        var headers = headerLine.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var hasHeader = headers.Contains("tag") || headers.Contains("date");
        var dataLines = hasHeader ? lines.Skip(1) : lines;

        if (hasHeader)
        {
            var tagIdx = Array.IndexOf(headers, "tag");
            var valIdx = Array.IndexOf(headers, "value");
            var dateIdx = Array.IndexOf(headers, "date");
            var descIdx = Array.IndexOf(headers, "description");

            foreach (var line in dataLines)
            {
                var trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var parts = SplitCsvLine(trimmed);
                rows.Add(new BatchRow
                {
                    Tag = tagIdx >= 0 && tagIdx < parts.Length ? parts[tagIdx] : "",
                    Date = dateIdx >= 0 && dateIdx < parts.Length ? parts[dateIdx] : "",
                    Value = valIdx >= 0 && valIdx < parts.Length ? parts[valIdx] : null,
                    Description = descIdx >= 0 && descIdx < parts.Length ? parts[descIdx] : null
                });
            }
        }
        else
        {
            // Positional: tag,value,date,description
            foreach (var line in dataLines)
            {
                var trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var parts = SplitCsvLine(trimmed);
                rows.Add(new BatchRow
                {
                    Tag = parts.Length > 0 ? parts[0] : "",
                    Value = parts.Length > 1 ? parts[1] : null,
                    Date = parts.Length > 2 ? parts[2] : "",
                    Description = parts.Length > 3 ? parts[3] : null
                });
            }
        }

        return rows;
    }

    private static string[] SplitCsvLine(string line)
    {
        // Simple CSV split with basic quoted field support
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        fields.Add(current.ToString().Trim());

        return fields.ToArray();
    }

    private static async Task<List<BatchRow>> ParseJsonAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var rows = JsonSerializer.Deserialize<List<BatchRow>>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return rows ?? [];
    }
}

public record BatchRow
{
    public string Tag { get; init; } = "";
    public string Date { get; init; } = "";
    public string? Value { get; init; }
    public string? Description { get; init; }
}

public class BatchImportResult
{
    public int Processed { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public List<BatchImportError> Errors { get; } = [];
}

public record BatchImportError
{
    public int Line { get; init; }
    public string Message { get; init; } = "";
}
