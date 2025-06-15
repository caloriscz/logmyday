using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IBackupService
{
    /// <summary>
    /// Exports all data to a JSON backup format
    /// </summary>
    /// <param name="userId">Optional user ID to filter data for specific user</param>
    /// <returns>BackupData containing all entities in JSON-serializable format</returns>
    Task<BackupData> ExportDataAsync(Guid? userId = null);

    /// <summary>
    /// Imports data from JSON backup format, avoiding duplicates
    /// </summary>
    /// <param name="backupData">The backup data to import</param>
    /// <param name="clearExistingData">Whether to clear existing data before import</param>
    /// <param name="userId">Optional user ID to associate imported data with</param>
    /// <returns>Import result with statistics</returns>
    Task<BackupImportResult> ImportDataAsync(BackupData backupData, bool clearExistingData = false, Guid? userId = null);

    /// <summary>
    /// Clears all data from the database
    /// </summary>
    /// <param name="userId">Optional user ID to clear data for specific user only</param>
    /// <returns>Number of records cleared</returns>
    Task<int> ClearDataAsync(Guid? userId = null);

    /// <summary>
    /// Validates backup data format and content
    /// </summary>
    /// <param name="backupData">The backup data to validate</param>
    /// <returns>Validation result</returns>
    Task<BackupValidationResult> ValidateBackupDataAsync(BackupData backupData);
}
