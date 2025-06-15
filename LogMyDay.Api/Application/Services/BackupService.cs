using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class BackupService : IBackupService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<BackupService> _logger;

    public BackupService(LogMyDayDbContext context, ILogger<BackupService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BackupData> ExportDataAsync(Guid? userId = null)
    {
        _logger.LogInformation("Starting data export for user: {UserId}", userId?.ToString() ?? "All users");

        try
        {
            // Export InputTypes (no user filtering needed as they're global)
            var inputTypes = await _context.InputTypes
                .Select(it => new InputTypeBackup
                {
                    Name = it.Name
                })
                .ToListAsync();

            // Export Patterns (no user filtering needed as they're global)
            var patterns = await _context.Patterns
                .Select(p => new PatternBackup
                {
                    Name = p.Name,
                    PatternValue = p.PatternValue,
                    Description = p.Description
                })
                .ToListAsync();

            // Export Tags with user filtering
            var tagsQuery = _context.Tags
                .Include(t => t.InputType)
                .Include(t => t.Pattern)
                .AsQueryable();

            if (userId.HasValue)
            {
                tagsQuery = tagsQuery.Where(t => t.UserId == userId);
            }

            var tags = await tagsQuery
                .Select(t => new TagBackup
                {
                    TagName = t.TagName,
                    InputTypeName = t.InputType != null ? t.InputType.Name : null,
                    IsRequired = t.IsRequired,
                    TimeGranularity = t.TimeGranularity,
                    IsRepeatable = t.IsRepeatable,
                    IsRange = t.IsRange,
                    PatternName = t.Pattern != null ? t.Pattern.Name : null,
                    UserId = t.UserId
                })
                .ToListAsync();

            // Export Activities with user filtering
            var activitiesQuery = _context.Activities
                .Include(a => a.Tag)
                .AsQueryable();

            if (userId.HasValue)
            {
                activitiesQuery = activitiesQuery.Where(a => a.UserId == userId);
            }

            var activities = await activitiesQuery
                .Select(a => new ActivityBackup
                {
                    DateCreated = a.DateCreated,
                    DateStarted = a.DateStarted,
                    DateFinished = a.DateFinished,
                    Description = a.Description,
                    TagName = a.Tag.TagName,
                    UserId = a.UserId
                })
                .ToListAsync();

            var backupData = new BackupData
            {
                Metadata = new BackupMetadata
                {
                    ExportDate = DateTime.UtcNow,
                    Version = "1.0",
                    TotalInputTypes = inputTypes.Count,
                    TotalPatterns = patterns.Count,
                    TotalTags = tags.Count,
                    TotalActivities = activities.Count
                },
                InputTypes = inputTypes,
                Patterns = patterns,
                Tags = tags,
                Activities = activities
            };

            _logger.LogInformation("Data export completed. Tags: {TagCount}, Activities: {ActivityCount}, InputTypes: {InputTypeCount}, Patterns: {PatternCount}",
                tags.Count, activities.Count, inputTypes.Count, patterns.Count);

            return backupData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during data export");
            throw;
        }
    }

    public async Task<BackupImportResult> ImportDataAsync(BackupData backupData, bool clearExistingData = false, Guid? userId = null)
    {
        _logger.LogInformation("Starting data import. Clear existing: {ClearExisting}, User: {UserId}", 
            clearExistingData, userId?.ToString() ?? "All users");

        var result = new BackupImportResult { Success = true };

        try
        {
            // Validate backup data first
            var validation = await ValidateBackupDataAsync(backupData);
            if (!validation.IsValid)
            {
                result.Success = false;
                result.Errors.AddRange(validation.Errors);
                result.Message = "Backup data validation failed";
                return result;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Clear existing data if requested
                if (clearExistingData)
                {
                    result.Statistics.RecordsCleared = await ClearDataAsync(userId);
                }

                // Import in order: InputTypes -> Patterns -> Tags -> Activities
                await ImportInputTypesAsync(backupData.InputTypes, result);
                await ImportPatternsAsync(backupData.Patterns, result);
                await ImportTagsAsync(backupData.Tags, result, userId);
                await ImportActivitiesAsync(backupData.Activities, result, userId);

                await transaction.CommitAsync();
                result.Message = "Data import completed successfully";

                _logger.LogInformation("Data import completed successfully. Statistics: {@Statistics}", result.Statistics);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during data import transaction");
                throw;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Import failed: {ex.Message}";
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "Data import failed");
        }

        return result;
    }

    public async Task<int> ClearDataAsync(Guid? userId = null)
    {
        _logger.LogInformation("Clearing data for user: {UserId}", userId?.ToString() ?? "All users");

        int recordsCleared = 0;

        try
        {
            // Clear activities first (due to foreign key constraints)
            var activitiesQuery = _context.Activities.AsQueryable();
            if (userId.HasValue)
            {
                activitiesQuery = activitiesQuery.Where(a => a.UserId == userId);
            }
            var activitiesToDelete = await activitiesQuery.ToListAsync();
            _context.Activities.RemoveRange(activitiesToDelete);
            recordsCleared += activitiesToDelete.Count;

            // Clear tags
            var tagsQuery = _context.Tags.AsQueryable();
            if (userId.HasValue)
            {
                tagsQuery = tagsQuery.Where(t => t.UserId == userId);
            }
            var tagsToDelete = await tagsQuery.ToListAsync();
            _context.Tags.RemoveRange(tagsToDelete);
            recordsCleared += tagsToDelete.Count;

            // Clear patterns and input types only if clearing all data (no user filter)
            if (!userId.HasValue)
            {
                var patternsToDelete = await _context.Patterns.ToListAsync();
                _context.Patterns.RemoveRange(patternsToDelete);
                recordsCleared += patternsToDelete.Count;

                var inputTypesToDelete = await _context.InputTypes.ToListAsync();
                _context.InputTypes.RemoveRange(inputTypesToDelete);
                recordsCleared += inputTypesToDelete.Count;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cleared {RecordsCleared} records", recordsCleared);
            return recordsCleared;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during data clearing");
            throw;
        }
    }

    public async Task<BackupValidationResult> ValidateBackupDataAsync(BackupData backupData)
    {
        var result = new BackupValidationResult { IsValid = true };

        if (backupData == null)
        {
            result.IsValid = false;
            result.Errors.Add("Backup data is null");
            return result;
        }

        // Validate metadata
        if (backupData.Metadata == null)
        {
            result.Warnings.Add("Metadata is missing");
        }

        // Validate tags have unique names
        var duplicateTagNames = backupData.Tags
            .GroupBy(t => t.TagName)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateTagNames.Any())
        {
            result.Errors.Add($"Duplicate tag names found: {string.Join(", ", duplicateTagNames)}");
            result.IsValid = false;
        }

        // Validate activities reference existing tags
        var tagNames = backupData.Tags.Select(t => t.TagName).ToHashSet();
        var invalidActivities = backupData.Activities
            .Where(a => !tagNames.Contains(a.TagName))
            .ToList();

        if (invalidActivities.Any())
        {
            result.Errors.Add($"Found {invalidActivities.Count} activities referencing non-existent tags");
            result.IsValid = false;
        }

        // Validate InputType references
        var inputTypeNames = backupData.InputTypes.Select(it => it.Name).ToHashSet();
        var invalidTagInputTypes = backupData.Tags
            .Where(t => !string.IsNullOrEmpty(t.InputTypeName) && !inputTypeNames.Contains(t.InputTypeName))
            .ToList();

        if (invalidTagInputTypes.Any())
        {
            result.Errors.Add($"Found {invalidTagInputTypes.Count} tags referencing non-existent input types");
            result.IsValid = false;
        }

        // Validate Pattern references
        var patternNames = backupData.Patterns.Select(p => p.Name).ToHashSet();
        var invalidTagPatterns = backupData.Tags
            .Where(t => !string.IsNullOrEmpty(t.PatternName) && !patternNames.Contains(t.PatternName))
            .ToList();

        if (invalidTagPatterns.Any())
        {
            result.Errors.Add($"Found {invalidTagPatterns.Count} tags referencing non-existent patterns");
            result.IsValid = false;
        }

        return result;
    }

    private async Task ImportInputTypesAsync(List<InputTypeBackup> inputTypes, BackupImportResult result)
    {
        var existingInputTypes = await _context.InputTypes
            .Select(it => it.Name)
            .ToHashSetAsync();

        foreach (var inputType in inputTypes)
        {
            if (existingInputTypes.Contains(inputType.Name))
            {
                result.Statistics.InputTypesSkipped++;
                continue;
            }

            var entity = new InputType
            {
                Name = inputType.Name
            };

            _context.InputTypes.Add(entity);
            result.Statistics.InputTypesImported++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task ImportPatternsAsync(List<PatternBackup> patterns, BackupImportResult result)
    {
        var existingPatterns = await _context.Patterns
            .Select(p => p.Name)
            .ToHashSetAsync();

        foreach (var pattern in patterns)
        {
            if (existingPatterns.Contains(pattern.Name))
            {
                result.Statistics.PatternsSkipped++;
                continue;
            }

            var entity = new Pattern
            {
                Name = pattern.Name,
                PatternValue = pattern.PatternValue,
                Description = pattern.Description
            };

            _context.Patterns.Add(entity);
            result.Statistics.PatternsImported++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task ImportTagsAsync(List<TagBackup> tags, BackupImportResult result, Guid? userId)
    {
        // Get lookup dictionaries for references
        var inputTypeLookup = await _context.InputTypes
            .ToDictionaryAsync(it => it.Name, it => it.Id);
        
        var patternLookup = await _context.Patterns
            .ToDictionaryAsync(p => p.Name, p => p.Id);

        var existingTagNames = await _context.Tags
            .Where(t => userId == null || t.UserId == userId)
            .Select(t => t.TagName)
            .ToHashSetAsync();

        foreach (var tag in tags)
        {
            if (existingTagNames.Contains(tag.TagName))
            {
                result.Statistics.TagsSkipped++;
                continue;
            }

            var entity = new Tag
            {
                TagName = tag.TagName,
                InputTypeId = !string.IsNullOrEmpty(tag.InputTypeName) && inputTypeLookup.ContainsKey(tag.InputTypeName) 
                    ? inputTypeLookup[tag.InputTypeName] : null,
                IsRequired = tag.IsRequired,
                TimeGranularity = tag.TimeGranularity,
                IsRepeatable = tag.IsRepeatable,
                IsRange = tag.IsRange,
                PatternId = !string.IsNullOrEmpty(tag.PatternName) && patternLookup.ContainsKey(tag.PatternName)
                    ? patternLookup[tag.PatternName] : null,
                UserId = userId ?? tag.UserId
            };

            _context.Tags.Add(entity);
            result.Statistics.TagsImported++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task ImportActivitiesAsync(List<ActivityBackup> activities, BackupImportResult result, Guid? userId)
    {
        // Get tag lookup
        var tagLookup = await _context.Tags
            .Where(t => userId == null || t.UserId == userId)
            .ToDictionaryAsync(t => t.TagName, t => t.Id);

        foreach (var activity in activities)
        {
            if (!tagLookup.ContainsKey(activity.TagName))
            {
                result.Warnings.Add($"Skipping activity with unknown tag: {activity.TagName}");
                result.Statistics.ActivitiesSkipped++;
                continue;
            }

            var entity = new Activity
            {
                DateCreated = activity.DateCreated,
                DateStarted = activity.DateStarted,
                DateFinished = activity.DateFinished,
                Description = activity.Description,
                TagId = tagLookup[activity.TagName],
                UserId = userId ?? activity.UserId
            };

            _context.Activities.Add(entity);
            result.Statistics.ActivitiesImported++;
        }

        await _context.SaveChangesAsync();
    }
}
