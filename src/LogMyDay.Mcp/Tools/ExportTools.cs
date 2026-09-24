using System.ComponentModel;
using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// The Excel export as the web offers it. The workbook comes back inline as a base64 blob — no
/// temp file, no second authenticated fetch, which suits the stateless transport; the client
/// writes it to disk.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class ExportTools(McpUserContext user, IExcelExportService exports, ITagService tags, UserClock clock)
{
    public const long MaxWorkbookBytes = 5_000_000;
    public const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [McpServerTool(Name = "preview_export", Title = "Preview export", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("What an Excel export would contain: days, activities, per-tag counts and the effective date range, without building the file.")]
    public async Task<ExcelExportStatistics> PreviewExport(
        [Description("Tag ids to include.")] List<int> tagIds,
        [Description("First day, yyyy-MM-dd; default the oldest activity.")] string? from = null,
        [Description("Last day, yyyy-MM-dd; default today.")] string? to = null,
        [Description("Daily (default), Weekly or Monthly rows.")] ExcelFormat format = ExcelFormat.Daily)
    {
        return await exports.GetExportPreview(await Request(tagIds, from, to, format, freezeFirstRow: false));
    }

    [McpServerTool(Name = "get_oldest_activity_date", Title = "Get oldest activity date", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("The date of the user's oldest activity, optionally among the given tags; null when there is none.")]
    public async Task<object> GetOldestActivityDate([Description("Tag ids to restrict to.")] List<int>? tagIds = null)
    {
        var ids = tagIds == null ? null : await OwnedIds(tagIds);
        var oldest = await exports.GetOldestActivityDate(user.UserId, ids);

        return new { oldestActivityDate = oldest };
    }

    [McpServerTool(Name = "generate_export", Title = "Generate Excel export", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Builds the Excel workbook for the given tags and range and returns it as an embedded xlsx blob (base64) plus the statistics. Save the blob to a .xlsx file. Workbooks over 5 MB fail with too-large; use the web export for those.")]
    public async Task<CallToolResult> GenerateExport(
        [Description("Tag ids to include.")] List<int> tagIds,
        [Description("First day, yyyy-MM-dd; default the oldest activity.")] string? from = null,
        [Description("Last day, yyyy-MM-dd; default today.")] string? to = null,
        [Description("Daily (default), Weekly or Monthly rows.")] ExcelFormat format = ExcelFormat.Daily,
        [Description("Freeze the header row in the workbook.")] bool freezeFirstRow = false)
    {
        var result = await exports.GenerateExcelReport(await Request(tagIds, from, to, format, freezeFirstRow));
        if (!result.Success || result.FileContent == null)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Message) ? "The export could not be generated." : result.Message);
        }

        if (result.FileContent.LongLength > MaxWorkbookBytes)
        {
            throw new PayloadTooLargeException(result.FileContent.LongLength, MaxWorkbookBytes, "Use the web Export page or POST /api/excel-export/generate (session or Basic auth) for a file this size.");
        }

        var summary = new { result.FileName, bytes = result.FileContent.Length, result.Statistics, result.Warnings };

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock { Text = JsonSerializer.Serialize(summary, McpJson.Options) },
                new EmbeddedResourceBlock
                {
                    Resource = BlobResourceContents.FromBytes(result.FileContent, $"logmyday://exports/{result.FileName}", XlsxMime)
                }
            ]
        };
    }

    // --- helpers ---

    /// <summary>The request always carries the caller's own user id; the tag ids are checked to be theirs.</summary>
    private async Task<ExcelExportRequest> Request(List<int> tagIds, string? from, string? to, ExcelFormat format, bool freezeFirstRow)
    {
        if (tagIds.Count == 0)
        {
            throw new ArgumentException("tagIds must name at least one tag.");
        }

        var request = new ExcelExportRequest
        {
            UserId = user.UserId,
            TagIds = await OwnedIds(tagIds),
            StartDate = from == null ? null : DateArguments.ParseDate(from, "from").ToDateTime(TimeOnly.MinValue),
            EndDate = to == null ? (await clock.Today(user.UserId)).ToDateTime(TimeOnly.MinValue) : DateArguments.ParseDate(to, "to").ToDateTime(TimeOnly.MinValue),
            Format = format,
            FreezeFirstRow = freezeFirstRow
        };

        if (request.StartDate > request.EndDate)
        {
            throw new ArgumentException("from must not be after to.");
        }

        return request;
    }

    private async Task<List<int>> OwnedIds(List<int> tagIds)
    {
        var ids = tagIds.Distinct().ToList();
        foreach (var id in ids)
        {
            await tags.GetTagById(id, user.UserId);
        }

        return ids;
    }
}
