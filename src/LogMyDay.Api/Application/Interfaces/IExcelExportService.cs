using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IExcelExportService
{
    /// <summary>
    /// Generates an Excel file with activities data for selected tags
    /// </summary>
    /// <param name="request">Export configuration including tag selection and date range</param>
    /// <returns>Excel file as byte array with export statistics</returns>
    Task<ExcelExportResult> GenerateExcelReport(ExcelExportRequest request);

    /// <summary>
    /// Gets available tags for Excel export selection
    /// </summary>
    /// <param name="userId">User ID to filter tags</param>
    /// <returns>List of available tags</returns>
    Task<List<TagResponse>> GetAvailableTags(Guid userId);

    /// <summary>
    /// Gets preview data for Excel export without generating the file
    /// </summary>
    /// <param name="request">Export configuration</param>
    /// <returns>Preview statistics and information</returns>
    Task<ExcelExportStatistics> GetExportPreview(ExcelExportRequest request);

    /// <summary>
    /// Gets the oldest activity date across all tags for the user
    /// </summary>
    /// <param name="userId">User ID to filter activities</param>
    /// <param name="tagIds">Optional tag IDs to filter activities</param>
    /// <returns>Oldest activity date or null if no activities exist</returns>
    Task<DateTime?> GetOldestActivityDate(Guid userId, List<int>? tagIds = null);

    /// <summary>
    /// Gets activities data for HTML export
    /// </summary>
    /// <param name="request">Export configuration including tag selection and date range</param>
    /// <returns>List of activity rows for HTML export</returns>
    Task<List<ActivityExportRow>> GetActivitiesForExport(ExcelExportRequest request);
}
