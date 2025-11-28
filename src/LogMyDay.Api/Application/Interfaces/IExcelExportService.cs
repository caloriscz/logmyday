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
    /// <param name="userId">Optional user ID to filter tags</param>
    /// <returns>List of available tags</returns>
    Task<List<TagResponse>> GetAvailableTags(Guid? userId = null);

    /// <summary>
    /// Gets preview data for Excel export without generating the file
    /// </summary>
    /// <param name="request">Export configuration</param>
    /// <returns>Preview statistics and information</returns>
    Task<ExcelExportStatistics> GetExportPreview(ExcelExportRequest request);
}
