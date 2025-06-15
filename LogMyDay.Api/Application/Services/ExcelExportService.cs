using ClosedXML.Excel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class ExcelExportService : IExcelExportService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<ExcelExportService> _logger;

    public ExcelExportService(LogMyDayDbContext context, ILogger<ExcelExportService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExcelExportResult> GenerateExcelReportAsync(ExcelExportRequest request)
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

            // Get tags with their names
            var selectedTags = await _context.Tags
                .Where(t => request.TagIds.Contains(t.Id))
                .Where(t => request.UserId == null || t.UserId == request.UserId)
                .Select(t => new { t.Id, t.TagName })
                .OrderBy(t => t.TagName)
                .ToListAsync();

            if (!selectedTags.Any())
            {
                result.Success = false;
                result.Message = "No valid tags found for the given selection";
                return result;
            }

            // Set date range
            var startDate = request.StartDate ?? DateTime.Today.AddMonths(-1);
            var endDate = request.EndDate ?? DateTime.Today;

            if (startDate > endDate)
            {
                result.Success = false;
                result.Message = "Start date cannot be after end date";
                return result;
            }

            // Get activities data
            var activitiesQuery = _context.Activities
                .Include(a => a.Tag)
                .Where(a => request.TagIds.Contains(a.TagId))
                .Where(a => a.DateStarted.Date >= startDate.Date && a.DateStarted.Date <= endDate.Date)
                .Where(a => request.UserId == null || a.UserId == request.UserId);

            var activities = await activitiesQuery
                .Select(a => new
                {
                    Date = a.DateStarted.Date,
                    TagId = a.TagId,
                    TagName = a.Tag.TagName,
                    Description = a.Description ?? "",
                    DateStarted = a.DateStarted,
                    DateFinished = a.DateFinished,
                    Duration = a.DateFinished != null 
                        ? (a.DateFinished.Value - a.DateStarted).TotalMinutes 
                        : (double?)null
                })
                .OrderBy(a => a.Date)
                .ThenBy(a => a.DateStarted)
                .ToListAsync();

            // Generate Excel file
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Daily Overview");            // Create headers
            worksheet.Cell(1, 1).Value = "Date";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;var columnIndex = 2;
            var tagColumnMap = new Dictionary<int, int>();

            foreach (var tag in selectedTags)
            {
                worksheet.Cell(1, columnIndex).Value = tag.TagName;
                worksheet.Cell(1, columnIndex).Style.Font.Bold = true;
                worksheet.Cell(1, columnIndex).Style.Fill.BackgroundColor = XLColor.LightGray;
                worksheet.Cell(1, columnIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(1, columnIndex).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                tagColumnMap[tag.Id] = columnIndex;
                columnIndex++;            }// Group activities by date, then create one row per date with combined values
            var groupedActivities = activities
                .GroupBy(a => a.Date)
                .OrderBy(g => g.Key)
                .ToList();

            var rowIndex = 2;
            var statisticsData = new Dictionary<string, int>();            foreach (var dayGroup in groupedActivities)
            {
                // Date column
                worksheet.Cell(rowIndex, 1).Value = dayGroup.Key;
                worksheet.Cell(rowIndex, 1).Style.NumberFormat.Format = "dd/mm/yyyy";

                // Group activities by tag for this day
                var tagActivities = dayGroup
                    .GroupBy(a => a.TagId)
                    .ToDictionary(g => g.Key, g => g.ToList());                // Fill in tag columns
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
                        
                        // Join multiple values with semicolon
                        var combinedValue = string.Join("; ", values);
                        worksheet.Cell(rowIndex, tagColumnIndex).Value = combinedValue;
                        
                        // Check if all values are numeric for right alignment
                        bool allNumeric = values.All(v => double.TryParse(v, out _));
                        if (allNumeric)
                        {
                            worksheet.Cell(rowIndex, tagColumnIndex).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        }
                        
                        // Update statistics
                        foreach (var activity in activitiesForTag)
                        {
                            if (!statisticsData.ContainsKey(activity.TagName))
                            {
                                statisticsData[activity.TagName] = 0;
                            }
                            statisticsData[activity.TagName]++;
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
            headerRange.Style.Font.Bold = true;            // Set data row height
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
            }// Create summary sheet
            var summarySheet = workbook.Worksheets.Add("Summary");
            
            summarySheet.Cell(1, 1).Value = "LogMyDay Activities Overview Report";
            summarySheet.Cell(1, 1).Style.Font.Bold = true;
            summarySheet.Cell(1, 1).Style.Font.FontSize = 16;

            summarySheet.Cell(3, 1).Value = "Report Period:";
            summarySheet.Cell(3, 1).Style.Font.Bold = true;
            summarySheet.Cell(3, 2).Value = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";

            summarySheet.Cell(4, 1).Value = "Selected Tags:";
            summarySheet.Cell(4, 1).Style.Font.Bold = true;
            summarySheet.Cell(4, 2).Value = string.Join(", ", selectedTags.Select(t => t.TagName));

            summarySheet.Cell(5, 1).Value = "Total Activities:";
            summarySheet.Cell(5, 1).Style.Font.Bold = true;
            summarySheet.Cell(5, 2).Value = activities.Count;

            summarySheet.Cell(6, 1).Value = "Total Days with Data:";
            summarySheet.Cell(6, 1).Style.Font.Bold = true;
            summarySheet.Cell(6, 2).Value = groupedActivities.Count;            summarySheet.Cell(7, 1).Value = "Report Format:";
            summarySheet.Cell(7, 1).Style.Font.Bold = true;
            summarySheet.Cell(7, 2).Value = "One row per date, multiple values per tag separated by semicolons";

            // Tag statistics
            summarySheet.Cell(9, 1).Value = "Activities by Tag:";
            summarySheet.Cell(9, 1).Style.Font.Bold = true;
            summarySheet.Cell(9, 1).Style.Fill.BackgroundColor = XLColor.LightGray;

            summarySheet.Cell(10, 1).Value = "Tag";
            summarySheet.Cell(10, 1).Style.Font.Bold = true;
            summarySheet.Cell(10, 2).Value = "Activity Count";
            summarySheet.Cell(10, 2).Style.Font.Bold = true;            var summaryRowIndex = 11;
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
            var tagNames = string.Join("-", selectedTags.Take(2).Select(t => t.TagName));
            if (selectedTags.Count > 2)
            {
                tagNames += $"-and-{selectedTags.Count - 2}-more";
            }
            result.FileName = $"logmyday-overview-{tagNames}-{startDate:yyyy-MM-dd}-to-{endDate:yyyy-MM-dd}.xlsx";

            // Set statistics
            result.Statistics = new ExcelExportStatistics
            {
                TotalDays = groupedActivities.Count,
                TotalActivities = activities.Count,
                SelectedTags = selectedTags.Count,
                DateRangeStart = startDate,
                DateRangeEnd = endDate,
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

    public async Task<List<TagResponse>> GetAvailableTagsAsync(Guid? userId = null)
    {
        var query = _context.Tags.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(t => t.UserId == userId);
        }

        return await query
            .Select(t => new TagResponse
            {
                Id = t.Id,
                Title = t.TagName,
                InputTypeId = t.InputTypeId,
                TypeId = t.InputTypeId,
                IsRequired = t.IsRequired,
                IsRepeatable = t.IsRepeatable,
                TimeGranularity = t.TimeGranularity,
                IsRange = t.IsRange
            })
            .OrderBy(t => t.Title)
            .ToListAsync();
    }

    public async Task<ExcelExportStatistics> GetExportPreviewAsync(ExcelExportRequest request)
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
                .Where(t => request.UserId == null || t.UserId == request.UserId)
                .Select(t => new { t.Id, t.TagName })
                .ToListAsync();

            var activitiesQuery = _context.Activities
                .Include(a => a.Tag)
                .Where(a => request.TagIds.Contains(a.TagId))
                .Where(a => a.DateStarted.Date >= startDate.Date && a.DateStarted.Date <= endDate.Date)
                .Where(a => request.UserId == null || a.UserId == request.UserId);

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
}
