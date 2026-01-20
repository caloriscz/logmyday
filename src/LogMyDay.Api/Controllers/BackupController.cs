using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LogMyDay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : BaseApiController
{
    private readonly IBackupService _backupService;
    private readonly ILogger<BackupController> _logger;

    public BackupController(IBackupService backupService, ILogger<BackupController> logger, IAuthService authService) : base(authService)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Export all data to JSON format
    /// </summary>
    /// <param name="userId">Optional user ID to filter data</param>
    /// <returns>JSON backup file</returns>
    [HttpGet("export")]
    public async Task<IActionResult> ExportData([FromQuery] Guid? userId_old = null)
    {
        try
        {
            Guid? userId = GetCurrentUserId();

            _logger.LogInformation("Export request received for user: {UserId}", userId?.ToString() ?? "All users");

            var backupData = await _backupService.ExportDataAsync(userId);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonContent = JsonSerializer.Serialize(backupData, jsonOptions);
            var fileName = $"logmyday-backup-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json";

            return File(System.Text.Encoding.UTF8.GetBytes(jsonContent), "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data export");

            return StatusCode(500, new { message = "Export failed", error = ex.Message });
        }
    }

    /// <summary>
    /// Import data from JSON backup file
    /// </summary>
    /// <param name="file">JSON backup file</param>
    /// <param name="clearExisting">Whether to clear existing data before import</param>
    /// <returns>Import result</returns>
    [HttpPost("import")]
    [Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
    public async Task<IActionResult> ImportData(
        IFormFile file,
        [FromQuery] bool clearExisting = false)
    {
        try
        {
            // Always use the current authenticated user's ID for security and data ownership
            var userId = GetCurrentUserId();

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided" });
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "File must be a JSON file" });
            }

            string jsonContent;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                jsonContent = await reader.ReadToEndAsync();
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            BackupData? backupData;
            try
            {
                backupData = JsonSerializer.Deserialize<BackupData>(jsonContent, jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON backup file");

                return BadRequest(new { message = "Invalid JSON format", error = ex.Message });
            }

            if (backupData == null)
            {
                return BadRequest(new { message = "Backup data is null or invalid" });
            }

            var result = await _backupService.ImportDataAsync(backupData, clearExisting, userId);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data import");

            return StatusCode(500, new { message = "Import failed", error = ex.Message });
        }
    }

    /// <summary>
    /// Clear all data from the database
    /// </summary>
    /// <param name="userId">Optional user ID to clear data for specific user only</param>
    /// <returns>Number of records cleared</returns>
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearData([FromQuery] Guid? userId = null)
    {
        try
        {
            _logger.LogInformation("Clear data request received for user: {UserId}", userId?.ToString() ?? "All users");

            var recordsCleared = await _backupService.ClearDataAsync(userId);

            return Ok(new
            {
                message = "Data cleared successfully",
                recordsCleared,
                userId = userId?.ToString() ?? "All users"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data clearing");

            return StatusCode(500, new { message = "Clear operation failed", error = ex.Message });
        }
    }

    /// <summary>
    /// Validate backup data format without importing
    /// </summary>
    /// <param name="file">JSON backup file to validate</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateBackup(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided" });
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "File must be a JSON file" });
            }

            _logger.LogInformation("Validation request received for file: {FileName}", file.FileName);

            string jsonContent;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                jsonContent = await reader.ReadToEndAsync();
            }

            JsonSerializerOptions jsonOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            BackupData? backupData;
            try
            {
                backupData = JsonSerializer.Deserialize<BackupData>(jsonContent, jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON backup file during validation");

                return BadRequest(new { message = "Invalid JSON format", error = ex.Message });
            }

            if (backupData == null)
            {
                return BadRequest(new { message = "Backup data is null or invalid" });
            }

            var validationResult = await _backupService.ValidateBackupData(backupData);
            return Ok(validationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backup validation");

            return StatusCode(500, new { message = "Validation failed", error = ex.Message });
        }
    }

    /// <summary>
    /// Get backup metadata and statistics
    /// </summary>
    /// <param name="userId">Optional user ID to get statistics for specific user</param>
    /// <returns>Current database statistics</returns>
    [HttpGet("info")]
    public async Task<IActionResult> GetBackupInfo([FromQuery] Guid? userId = null)
    {
        try
        {
            _logger.LogInformation("Backup info request received for user: {UserId}", userId?.ToString() ?? "All users");

            var backupData = await _backupService.ExportDataAsync(userId);

            return Ok(new
            {
                metadata = backupData.Metadata,
                currentDateTime = DateTime.UtcNow,
                userId = userId?.ToString() ?? "All users"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting backup info");

            return StatusCode(500, new { message = "Failed to get backup info", error = ex.Message });
        }
    }

    // NEW: Secure user-scoped backup endpoints (v2.0)

    /// <summary>
    /// Create a secure backup of current user's activities and tags (excluding user credentials)
    /// Requires authentication - only backs up data for the authenticated user
    /// </summary>
    [HttpGet("secure/export")]
    [Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
    public async Task<ActionResult<SecureBackupDto>> CreateSecureBackup()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Secure backup export request from user: {UserId}", userId);

            var backup = await _backupService.CreateSecureBackup(userId);

            return Ok(backup);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during secure backup export");
            return StatusCode(500, new { message = "Failed to create secure backup", error = ex.Message });
        }
    }

    /// <summary>
    /// Restore activities and tags from secure backup, assigning them to the current authenticated user
    /// Requires authentication - all restored data will be assigned to the authenticated user
    /// </summary>
    [HttpPost("secure/restore")]
    [Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
    public async Task<ActionResult<BackupImportResult>> RestoreSecureBackup([FromBody] SecureBackupDto backup)
    {
        if (backup == null)
        {
            return BadRequest("Backup data is required");
        }

        try
        {
            var userId = GetCurrentUserId();
            var result = await _backupService.RestoreSecureBackup(backup, userId);

            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during secure backup restore");
            return StatusCode(500, new { message = "Failed to restore secure backup", error = ex.Message });
        }
    }

    /// <summary>
    /// Clear ALL data for the current authenticated user only (activities, tags, etc.)
    /// Requires authentication - only clears data for the authenticated user, preserves other users' data
    /// </summary>
    [HttpDelete("secure/clear-user-data")]
    [Authorize(AuthenticationSchemes = "lmd-cookie,basic")]
    public async Task<ActionResult> ClearCurrentUserData()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("Secure user data clear request from user: {UserId}", userId);

            var recordsCleared = await _backupService.ClearUserData(userId);

            return Ok(new
            {
                message = "All your data has been cleared successfully. Other users' data remains intact.",
                recordsCleared,
                userId = userId.ToString()
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during secure user data clearing");

            return StatusCode(500, new { message = "Failed to clear user data", error = ex.Message });
        }
    }
}
