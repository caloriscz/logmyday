using System.ComponentModel;
using System.Text;
using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Backup and restore of the user's own data. Exports travel inline as JSON up to a cap; anything
/// larger belongs to the REST download. Restore and clear are guarded by sentinels because they
/// replace or destroy everything the user has logged.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class BackupTools(McpUserContext user, IBackupService backups, IUserService users)
{
    public const long MaxInlineBytes = 1_000_000;
    public const string RestoreSentinel = "RESTORE_LMD_BACKUP";
    public const string ClearSentinel = "CLEAR_MY_DATA";
    public const string ClearAllSentinel = "CLEAR_ALL_USERS_DATA";

    [McpServerTool(Name = "get_backup_info", Title = "Get backup info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Counts of what a backup of the user's data would contain (tags, activities, groups, option lists, reminders, todo lists, …).")]
    public async Task<object> GetBackupInfo()
    {
        var data = await backups.ExportDataAsync(user.UserId);

        return new { metadata = data.Metadata, currentDateTime = DateTime.UtcNow, userId = user.UserId };
    }

    [McpServerTool(Name = "export_secure_backup", Title = "Export secure backup", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("The user's data as a secure backup document (activities, tags, groups, option lists, notifications, scan mappings; no credentials), inline as JSON. Over 1 MB the call fails with too-large and points to the REST download.")]
    public async Task<SecureBackupDto> ExportSecureBackup()
    {
        var backup = await backups.CreateSecureBackup(user.UserId);
        EnforceCap(backup, "Download it with GET /api/backup/secure/export (session or Basic auth) instead.");

        return backup;
    }

    [McpServerTool(Name = "validate_backup", Title = "Validate backup", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Checks a full-format backup document (the shape of GET /api/backup/export, version 2.0) for structural problems without importing it. Secure backups (version 2.1, from export_secure_backup) are validated by restore_secure_backup itself.")]
    public async Task<BackupValidationResult> ValidateBackup([Description("The backup document as JSON.")] JsonElement backup)
    {
        return await backups.ValidateBackupData(Deserialize<BackupData>(backup));
    }

    [McpServerTool(Name = "restore_secure_backup", Title = "Restore secure backup", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Restores a secure backup document (from export_secure_backup) into the user's account, merging by matching names and skipping duplicates. Requires confirm = \"RESTORE_LMD_BACKUP\". Ask the user before confirming.")]
    public async Task<BackupImportResult> RestoreSecureBackup(
        [Description("The secure backup document as JSON.")] JsonElement backup,
        [Description("Must be exactly RESTORE_LMD_BACKUP.")] string? confirm = null)
    {
        var document = Deserialize<SecureBackupDto>(backup);
        ConfirmSentinel.Require(confirm, RestoreSentinel,
            $"Restores {document.Activities.Count} activities and {document.Tags.Count} tags from a backup created {document.CreatedAt:u} into this account.");

        return await backups.RestoreSecureBackup(document, user.UserId);
    }

    [McpServerTool(Name = "clear_user_data", Title = "Clear my data", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes ALL of the user's logged data — activities, tags, groups, option lists, reminders, todo lists, scan mappings — keeping the account. Requires confirm = \"CLEAR_MY_DATA\". Take export_secure_backup first and ask the user before confirming.")]
    public async Task<object> ClearUserData([Description("Must be exactly CLEAR_MY_DATA.")] string? confirm = null)
    {
        var info = await backups.ExportDataAsync(user.UserId);
        ConfirmSentinel.Require(confirm, ClearSentinel,
            $"Deletes {info.Metadata.TotalActivities} activities, {info.Metadata.TotalTags} tags and everything else logged by this user.");

        var cleared = await backups.ClearUserData(user.UserId);

        return new { cleared = true, recordsCleared = cleared };
    }

    [McpServerTool(Name = "admin_export_backup", Title = "Export a user's backup (admin)", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Admin)]
    [Description("Admin: a full-format backup document (version 2.0) of one user's data, inline as JSON (1 MB cap → too-large). Contains entities only — no accounts, password hashes, API keys or settings.")]
    public async Task<BackupData> AdminExportBackup([Description("The user's id.")] Guid userId)
    {
        _ = await users.Get(userId, CancellationToken.None) ?? throw new KeyNotFoundException("User not found");
        var data = await backups.ExportDataAsync(userId);
        EnforceCap(data, "Have that user download it with GET /api/backup/export instead.");

        return data;
    }

    [McpServerTool(Name = "admin_clear_all_data", Title = "Clear all users' data (admin)", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Admin)]
    [Description("Admin: deletes the logged data of EVERY user on this server (accounts are kept). Requires confirm = \"CLEAR_ALL_USERS_DATA\". Never call this without an explicit instruction from the user naming every account affected.")]
    public async Task<object> AdminClearAllData([Description("Must be exactly CLEAR_ALL_USERS_DATA.")] string? confirm = null)
    {
        var accounts = (await users.List(CancellationToken.None)).Count;
        ConfirmSentinel.Require(confirm, ClearAllSentinel, $"Deletes the logged data of all {accounts} user accounts on this server.");

        var cleared = await backups.ClearDataAsync(null);

        return new { cleared = true, recordsCleared = cleared, accountsAffected = accounts };
    }

    private static void EnforceCap(object document, string hint)
    {
        var bytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(document, McpJson.Options));
        if (bytes > MaxInlineBytes)
        {
            throw new PayloadTooLargeException(bytes, MaxInlineBytes, hint);
        }
    }

    private static T Deserialize<T>(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("backup must be a JSON object.");
        }

        try
        {
            return element.Deserialize<T>(McpJson.Options) ?? throw new ArgumentException("backup is empty.");
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"backup is not a valid document: {ex.Message}");
        }
    }
}
