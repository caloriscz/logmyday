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
    Task<BackupValidationResult> ValidateBackupData(BackupData backupData);

    /// <summary>
    /// Creates a secure backup of the current user's data (activities and tags only, no user credentials)
    /// </summary>
    /// <param name="userId">The authenticated user's ID</param>
    /// <returns>Secure backup data</returns>
    Task<SecureBackupDto> CreateSecureBackup(Guid userId);
    
    /// <summary>
    /// Restores data from secure backup and assigns it to the specified user
    /// </summary>
    /// <param name="backup">The secure backup data to restore</param>
    /// <param name="userId">The authenticated user's ID to assign restored data to</param>
    /// <returns>Restore operation result</returns>
    Task<BackupImportResult> RestoreSecureBackup(SecureBackupDto backup, Guid userId);
    
    /// <summary>
    /// Clears all data for the specified user only (preserves other users' data)
    /// </summary>
    /// <param name="userId">The authenticated user's ID</param>
    /// <returns>Number of records cleared</returns>
    Task<int> ClearUserData(Guid userId);
}
