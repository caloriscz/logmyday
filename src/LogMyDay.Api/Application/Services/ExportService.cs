using System.Globalization;
using ClosedXML.Excel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class ExportService : IExcelExportService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<ExportService> _logger;

    public ExportService(LogMyDayDbContext context, ILogger<ExportService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExcelExportResult> GenerateExcelReport(ExcelExportRequest request)
    {
        _logger.LogInformation("Starting Excel report generation for {TagCount} tags", request.TagIds.Count);

        var result = new ExcelExportResult { Success = true };

        try
        {
            // Validate request
            if (!request.TagIds.Any())
            {
                result.Success = false;
                result.Message = "At least one tag must be selected";
                return result;
            }

            // Get tags with their names - enforce user-specific filtering
            var rawTags = await _context.Tags
                .Include(t => t.Group)
                .Where(t => request.TagIds.Contains(t.Id))
                .Where(t => t.UserId == request.UserId)
                .ToListAsync();

            var selectedTags = rawTags
                .Select(t => new
                {
                    t.Id,
                    t.TagName,
                    DisplayName = t.Group != null ? $"{t.Group.Name}: {t.TagName}" : t.TagName,
                    t.IsComputed
                })
                .OrderBy(t => t.DisplayName)
                .ToList();

            if (!selectedTags.Any())
            {
                result.Success = false;
                result.Message = "No valid tags found for the given selection";
                return result;
            }

            // Validate date range if provided
            if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            {
                result.Success = false;
                result.Message = "Start date cannot be after end date";
                return result;
            }

            // Get activities data - start with base query
            var activitiesQuery = _context.Activities
                .Include(a => a.Tag)
                .Where(a => request.TagIds.Contains(a.TagId))
                .Where(a => a.UserId == request.UserId);

            // Only filter by date if dates are provided (not null)
            if (request.StartDate.HasValue && request.EndDate.HasValue)
            {
                activitiesQuery = activitiesQuery.Where(a => a.DateStarted.Date >= request.StartDate.Value.Date && a.DateStarted.Date <= request.EndDate.Value.Date);
            }

            var activities = await activitiesQuery
                .Select(a => new
                {
                    a.DateStarted.Date,
                    a.TagId,
                    a.Tag.TagName,
                    Description = a.Description ?? "",
                    a.DateStarted,
                    a.DateFinished,
                    Duration = a.DateFinished != null
                        ? (a.DateFinished.Value - a.DateStarted).TotalMinutes
                        : (double?)null
                })
                .OrderBy(a => a.Date)
                .ThenBy(a => a.DateStarted)
                .ToListAsync();

            // Generate Excel file
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Daily Overview");
            worksheet.Cell(1, 1).Value = "Date";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center; var columnIndex = 2;
            var tagColumnMap = new Dictionary<int, int>();

            var tagDisplayNames = selectedTags.ToDictionary(t => t.Id, t => t.DisplayName);

            foreach (var tag in selectedTags)
            {
                // A computed tag's values are calculated by a Tag Activity Relations rule.
                worksheet.Cell(1, columnIndex).Value = tag.IsComputed ? $"{tag.DisplayName} (computed)" : tag.DisplayName;
                worksheet.Cell(1, columnIndex).Style.Font.Bold = true;
                worksheet.Cell(1, columnIndex).Style.Fill.BackgroundColor = XLColor.LightGray;
                worksheet.Cell(1, columnIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(1, columnIndex).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                tagColumnMap[tag.Id] = columnIndex;
                columnIndex++;
            }// Group activities by date, then create one row per date with combined values
            var groupedActivities = activities
                .GroupBy(a => a.Date)
                .OrderBy(g => g.Key)
                .ToList();

            var rowIndex = 2;
            var statisticsData = new Dictionary<string, int>(); foreach (var dayGroup in groupedActivities)
            {
                // Date column
                worksheet.Cell(rowIndex, 1).Value = dayGroup.Key;
                worksheet.Cell(rowIndex, 1).Style.NumberFormat.Format = "dd/mm/yyyy";

                // Group activities by tag for this day
                var tagActivities = dayGroup
                    .GroupBy(a => a.TagId)
                    .ToDictionary(g => g.Key, g => g.ToList());
                foreach (var tagCol in tagColumnMap)
                {
                    var tagId = tagCol.Key;
                    var tagColumnIndex = tagCol.Value;

                    if (tagActivities.ContainsKey(tagId))
                    {
                        var activitiesForTag = tagActivities[tagId];
                        var values = new List<string>();

                        foreach (var activity in activitiesForTag.OrderBy(a => a.DateStarted))
                        {
                            string cellValue = "";

                            // Try to determine the best value to display
                            if (!string.IsNullOrWhiteSpace(activity.Description))
                            {
                                cellValue = activity.Description;
                            }
                            else if (activity.Duration.HasValue)
                            {
                                cellValue = Math.Round(activity.Duration.Value, 2).ToString();
                            }
                            else
                            {
                                cellValue = "1";
                            }

                            values.Add(cellValue);
                        }

                        // If single numeric value, store as number; otherwise as text
                        if (values.Count == 1 && double.TryParse(values[0], out double numericValue))
                        {
                            worksheet.Cell(rowIndex, tagColumnIndex).Value = numericValue;
                            worksheet.Cell(rowIndex, tagColumnIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        }
                        else
                        {
                            // Join multiple values with semicolon
                            var combinedValue = string.Join("; ", values);
                            worksheet.Cell(rowIndex, tagColumnIndex).Value = combinedValue;

                            // Check if all values are numeric for right alignment
                            bool allNumeric = values.All(v => double.TryParse(v, out _));
                            if (allNumeric)
                            {
                                worksheet.Cell(rowIndex, tagColumnIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                            }
                        }

                        // Update statistics
                        foreach (var activity in activitiesForTag)
                        {
                            var displayName = tagDisplayNames.GetValueOrDefault(tagId, activity.TagName);
                            if (!statisticsData.ContainsKey(displayName))
                            {
                                statisticsData[displayName] = 0;
                            }
                            statisticsData[displayName]++;
                        }
                    }
                    else
                    {
                        // Clear cell if no activities for this tag on this day
                        worksheet.Cell(rowIndex, tagColumnIndex).Value = "";
                    }
                }

                rowIndex++;
            }

            // Apply formatting
            // Set header row height and padding
            worksheet.Row(1).Height = 50; // More height for headers
            var headerRange = worksheet.Range(1, 1, 1, columnIndex - 1);
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Font.Bold = true;

            if (rowIndex > 2)
            {
                for (int r = 2; r < rowIndex; r++)
                {
                    worksheet.Row(r).Height = 30; // Approximately 40 pixels
                }

                // Set vertical alignment for all data cells
                var dataRange = worksheet.Range(2, 1, rowIndex - 1, columnIndex - 1);
                dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }            // Auto-fit columns but with minimum width
            worksheet.ColumnsUsed().AdjustToContents();
            foreach (var column in worksheet.ColumnsUsed())
            {
                if (column.Width < 12)
                {
                    column.Width = 12; // Minimum column width
                }

                if (column.Width > 25)
                {
                    column.Width = 25; // Maximum column width
                }
            }

            // Add borders to the data table
            if (rowIndex > 1)
            {
                var tableRange = worksheet.Range(1, 1, rowIndex - 1, columnIndex - 1);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            // Freeze first row if requested
            if (request.FreezeFirstRow && rowIndex > 1)
            {
                worksheet.SheetView.FreezeRows(1);
            }

            // Records: values measured together stay on one row of their record type's sheet.
            await AddRecordSheets(workbook, request, selectedTags.Select(t => t.Id).ToList());

            // Create summary sheet
            var summarySheet = workbook.Worksheets.Add("Summary");

            summarySheet.Cell(1, 1).Value = "LogMyDay Activities Overview Report";
            summarySheet.Cell(1, 1).Style.Font.Bold = true;
            summarySheet.Cell(1, 1).Style.Font.FontSize = 16;

            summarySheet.Cell(3, 1).Value = "Report Period:";
            summarySheet.Cell(3, 1).Style.Font.Bold = true;
            summarySheet.Cell(3, 2).Value = request.StartDate.HasValue && request.EndDate.HasValue 
                ? $"{request.StartDate.Value:dd/MM/yyyy} - {request.EndDate.Value:dd/MM/yyyy}"
                : "All available data";

            summarySheet.Cell(4, 1).Value = "Selected Tags:";
            summarySheet.Cell(4, 1).Style.Font.Bold = true;
            summarySheet.Cell(4, 2).Value = string.Join(", ", selectedTags.Select(t => t.DisplayName));

            summarySheet.Cell(5, 1).Value = "Total Activities:";
            summarySheet.Cell(5, 1).Style.Font.Bold = true;
            summarySheet.Cell(5, 2).Value = activities.Count;

            summarySheet.Cell(6, 1).Value = "Total Days with Data:";
            summarySheet.Cell(6, 1).Style.Font.Bold = true;
            summarySheet.Cell(6, 2).Value = groupedActivities.Count; summarySheet.Cell(7, 1).Value = "Report Format:";
            summarySheet.Cell(7, 1).Style.Font.Bold = true;
            summarySheet.Cell(7, 2).Value = "One row per date, multiple values per tag separated by semicolons";

            // Tag statistics
            summarySheet.Cell(9, 1).Value = "Activities by Tag:";
            summarySheet.Cell(9, 1).Style.Font.Bold = true;
            summarySheet.Cell(9, 1).Style.Fill.BackgroundColor = XLColor.LightGray;

            summarySheet.Cell(10, 1).Value = "Tag";
            summarySheet.Cell(10, 1).Style.Font.Bold = true;
            summarySheet.Cell(10, 2).Value = "Activity Count";
            summarySheet.Cell(10, 2).Style.Font.Bold = true; var summaryRowIndex = 11;
            foreach (var tagStat in statisticsData.OrderByDescending(kvp => kvp.Value))
            {
                summarySheet.Cell(summaryRowIndex, 1).Value = tagStat.Key;
                summarySheet.Cell(summaryRowIndex, 2).Value = tagStat.Value;
                summaryRowIndex++;
            }

            summarySheet.ColumnsUsed().AdjustToContents();

            // Save to memory stream
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            result.FileContent = stream.ToArray();

            // Generate filename
            var tagNames = string.Join("-", selectedTags.Take(2).Select(t => t.TagName.Replace(" ", "-")));
            if (selectedTags.Count > 2)
            {
                tagNames += $"-and-{selectedTags.Count - 2}-more";
            }
            var dateRange = request.StartDate.HasValue && request.EndDate.HasValue
                ? $"{request.StartDate.Value:yyyy-MM-dd}-to-{request.EndDate.Value:yyyy-MM-dd}"
                : "all-data";
            result.FileName = $"logmyday-overview-{tagNames}-{dateRange}.xlsx";

            // Set statistics
            result.Statistics = new ExcelExportStatistics
            {
                TotalDays = groupedActivities.Count,
                TotalActivities = activities.Count,
                SelectedTags = selectedTags.Count,
                DateRangeStart = request.StartDate ?? activities.Min(a => a.DateStarted.Date),
                DateRangeEnd = request.EndDate ?? activities.Max(a => a.DateStarted.Date),
                TagActivityCounts = statisticsData
            };

            result.Message = $"Excel overview report generated successfully with {activities.Count} activities across {groupedActivities.Count} days";

            _logger.LogInformation("Excel report generated successfully: {FileName}, {ActivityCount} activities",
                result.FileName, activities.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Excel report");
            result.Success = false;
            result.Message = $"Failed to generate Excel report: {ex.Message}";
            return result;
        }
    }

    // One sheet per record type with a selected field: a row per record (date, time) and a column
    // per field in form order, so values measured together stay together.
    private async Task AddRecordSheets(XLWorkbook workbook, ExcelExportRequest request, IReadOnlyCollection<int> selectedTagIds)
    {
        var types = await _context.Tags
            .AsNoTracking()
            .Include(t => t.RecordFields).ThenInclude(f => f.Unit)
            .Where(t => t.UserId == request.UserId && t.IsRecordType && t.RecordFields.Any(f => selectedTagIds.Contains(f.Id)))
            .OrderBy(t => t.TagName)
            .ToListAsync();

        foreach (var type in types)
        {
            var fields = type.RecordFields.OrderBy(f => f.RecordFieldOrder).ThenBy(f => f.Id).ToList();
            var recordsQuery = _context.Records
                .AsNoTracking()
                .Include(r => r.Values)
                .Where(r => r.UserId == request.UserId && r.RecordTypeId == type.Id);
            if (request.StartDate.HasValue && request.EndDate.HasValue)
            {
                recordsQuery = recordsQuery.Where(r => r.DateStarted.Date >= request.StartDate.Value.Date && r.DateStarted.Date <= request.EndDate.Value.Date);
            }

            var records = await recordsQuery.OrderBy(r => r.DateStarted).ThenBy(r => r.Id).ToListAsync();

            var sheet = workbook.Worksheets.Add(SheetName(workbook, type.TagName));
            sheet.Cell(1, 1).Value = "Date";
            sheet.Cell(1, 2).Value = "Time";
            for (var i = 0; i < fields.Count; i++)
            {
                var unit = fields[i].Unit?.Symbol;
                sheet.Cell(1, i + 3).Value = RecordFieldValues.FieldName(type.TagName, fields[i].TagName) + (unit != null ? $" ({unit})" : string.Empty);
            }

            var header = sheet.Range(1, 1, 1, fields.Count + 2);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;

            var row = 2;
            foreach (var record in records)
            {
                sheet.Cell(row, 1).Value = record.DateStarted.Date;
                sheet.Cell(row, 1).Style.NumberFormat.Format = "dd/mm/yyyy";
                sheet.Cell(row, 2).Value = record.DateStarted.ToString("HH:mm", CultureInfo.InvariantCulture);
                for (var i = 0; i < fields.Count; i++)
                {
                    var value = record.Values.FirstOrDefault(v => v.TagId == fields[i].Id)?.Description;
                    if (value == null)
                    {
                        continue;
                    }

                    var isNumber = fields[i].OptionListId == null && TagRuleInputTypes.IsNumeric(fields[i].InputTypeId)
                        && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                    sheet.Cell(row, i + 3).Value = isNumber ? double.Parse(value, CultureInfo.InvariantCulture) : value;
                }

                row++;
            }

            sheet.ColumnsUsed().AdjustToContents();
            if (request.FreezeFirstRow)
            {
                sheet.SheetView.FreezeRows(1);
            }
        }
    }

    // Excel sheet names have at most 31 characters, none of : \ / ? * [ ], and are unique.
    private static string SheetName(XLWorkbook workbook, string name)
    {
        var clean = new string(name.Where(c => !":\\/?*[]".Contains(c)).ToArray()).Trim();
        clean = clean.Length == 0 ? "Record" : clean;
        clean = clean.Length > 31 ? clean[..31] : clean;

        var candidate = clean;
        for (var n = 2; workbook.Worksheets.Contains(candidate); n++)
        {
            var suffix = $" ({n})";
            candidate = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] : clean) + suffix;
        }

        return candidate;
    }

    public async Task<List<TagResponse>> GetAvailableTags(Guid userId)
    {
        // A record type holds no values, so it has nothing to export; its fields are listed.
        var tags = await _context.Tags
            .Include(t => t.Group)
            .Where(t => t.UserId == userId && !t.IsRecordType)
            .ToListAsync();

        return tags
            .Select(t => new TagResponse
            {
                Id = t.Id,
                Title = t.Group != null ? $"{t.Group.Name}: {t.TagName}" : t.TagName,
                GroupId = t.Group?.Id,
                GroupName = t.Group?.Name,
                InputTypeId = t.InputTypeId,
                TypeId = t.InputTypeId,
                IsRequired = t.IsRequired,
                IsRepeatable = t.IsRepeatable,
                TimeGranularity = t.TimeGranularity,
                IsRange = t.IsRange
            })
            .OrderBy(t => t.GroupName)
            .ThenBy(t => t.Title)
            .ToList();
    }

    public async Task<ExcelExportStatistics> GetExportPreview(ExcelExportRequest request)
    {
        var statistics = new ExcelExportStatistics();

        try
        {
            if (!request.TagIds.Any())
            {
                return statistics;
            }

            var startDate = request.StartDate ?? DateTime.Today.AddMonths(-1);
            var endDate = request.EndDate ?? DateTime.Today;

            var selectedTags = await _context.Tags
                .Where(t => request.TagIds.Contains(t.Id))
                .Where(t => t.UserId == request.UserId)
                .Select(t => new { t.Id, t.TagName })
                .ToListAsync();

            var activitiesQuery = _context.Activities
                .Include(a => a.Tag)
                .Where(a => request.TagIds.Contains(a.TagId))
                .Where(a => a.DateStarted.Date >= startDate.Date && a.DateStarted.Date <= endDate.Date)
                .Where(a => a.UserId == request.UserId);

            var activities = await activitiesQuery
                .Select(a => new { a.Tag.TagName, Date = a.DateStarted.Date })
                .ToListAsync();

            var tagCounts = activities
                .GroupBy(a => a.TagName)
                .ToDictionary(g => g.Key, g => g.Count());

            var uniqueDays = activities
                .Select(a => a.Date)
                .Distinct()
                .Count();

            statistics.TotalDays = uniqueDays;
            statistics.TotalActivities = activities.Count;
            statistics.SelectedTags = selectedTags.Count;
            statistics.DateRangeStart = startDate;
            statistics.DateRangeEnd = endDate;
            statistics.TagActivityCounts = tagCounts;

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating export preview");
            return statistics;
        }
    }

    public async Task<DateTime?> GetOldestActivityDate(Guid userId, List<int>? tagIds = null)
    {
        try
        {
            var query = _context.Activities
                .Where(a => a.UserId == userId);

            if (tagIds != null && tagIds.Any())
            {
                query = query.Where(a => tagIds.Contains(a.TagId));
            }

            var oldestDate = await query
                .OrderBy(a => a.DateStarted)
                .Select(a => (DateTime?)a.DateStarted)
                .FirstOrDefaultAsync();

            return oldestDate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching oldest activity date");
            return null;
        }
    }

    public async Task<List<ActivityExportRow>> GetActivitiesForExport(ExcelExportRequest request)
    {
        try
        {
            var query = _context.Activities
                .Include(a => a.Tag)
                .Where(a => request.TagIds.Contains(a.TagId))
                .Where(a => a.UserId == request.UserId);

            // Only filter by date if both dates are provided (not null)
            if (request.StartDate.HasValue && request.EndDate.HasValue)
            {
                query = query.Where(a => a.DateStarted.Date >= request.StartDate.Value.Date && a.DateStarted.Date <= request.EndDate.Value.Date);
            }

            var rawActivities = await query
                .Include(a => a.Tag)
                    .ThenInclude(t => t.Group)
                .Include(a => a.Tag)
                    .ThenInclude(t => t.RecordType)
                .OrderByDescending(a => a.DateStarted)
                .ToListAsync();

            var activities = rawActivities
                .Select(a => new ActivityExportRow
                {
                    DateStarted = a.DateStarted,
                    Tag = a.Tag.Group != null ? $"{a.Tag.Group.Name}: {a.Tag.TagName}" : a.Tag.TagName,
                    Description = a.Description ?? "",
                    TimeGranularity = (int)a.Tag.TimeGranularity,
                    Generated = a.RuleId != null,
                    Record = a.RecordId is int recordId ? $"{a.Tag.RecordType?.TagName ?? "Record"} #{recordId}" : null
                })
                .ToList();

            return activities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching activities for export");
            return new List<ActivityExportRow>();
        }
    }
}
