using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface ISecureBackupApi
{
    /// <summary>
    /// Create a secure backup of current user's activities and tags (excluding user credentials)
    /// </summary>
    [Get("/api/backup/secure/export")]
    Task<SecureBackupDto> CreateSecureBackupAsync();
    
    /// <summary>
    /// Restore activities and tags from secure backup, assigning them to the current user
    /// </summary>
    [Post("/api/backup/secure/restore")]
    Task<BackupImportResult> RestoreSecureBackupAsync([Body] SecureBackupDto backup);
    
    /// <summary>
    /// Clear ALL data for the current user only (activities, tags, etc.)
    /// </summary>
    [Delete("/api/backup/secure/clear-user-data")]
    Task<object> ClearCurrentUserDataAsync();
}