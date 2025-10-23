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

            // Export Units (global, no user filtering - include all units)
            var units = await _context.Units
                .Include(u => u.Quantity)
                .Select(u => new UnitBackup
                {
                    Key = u.Key,
                    Symbol = u.Symbol,
                    AToBase = u.AToBase,
                    BToBase = u.BToBase,
                    Decimals = u.Decimals,
                    QuantityKey = u.Quantity != null ? u.Quantity.Key : null
                })
                .ToListAsync();

            // Export TagOptionLists with user filtering
            var tagOptionListsQuery = _context.TagOptionLists.AsQueryable();
            if (userId.HasValue)
            {
                tagOptionListsQuery = tagOptionListsQuery.Where(ol => ol.UserId == userId);
            }

            var tagOptionLists = await tagOptionListsQuery
                .Select(ol => new TagOptionListBackup
                {
                    Name = ol.Name,
                    UserId = ol.UserId
                })
                .ToListAsync();

            // Export TagOptions with user filtering (via TagOptionList)
            var tagOptionsQuery = _context.TagOptions
                .Include(to => to.OptionList)
                .AsQueryable();
            
            if (userId.HasValue)
            {
                tagOptionsQuery = tagOptionsQuery.Where(to => to.OptionList != null && to.OptionList.UserId == userId);
            }

            var tagOptions = await tagOptionsQuery
                .Select(to => new TagOptionBackup
                {
                    Value = to.Value,
                    DisplayName = to.DisplayName,
                    TagOptionListKey = to.OptionList != null ? to.OptionList.Name : string.Empty
                })
                .ToListAsync();

            // Export Tags with user filtering
            var tagsQuery = _context.Tags
                .Include(t => t.InputType)
                .Include(t => t.Pattern)
                .Include(t => t.Unit)
                .Include(t => t.OptionList)
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
                    UserId = t.UserId,
                    UnitKey = t.Unit != null ? t.Unit.Key : null,
                    UnitSymbol = t.Unit != null ? t.Unit.Symbol : null,
                    MinValue = t.MinValue,
                    MaxValue = t.MaxValue,
                    Step = t.Step,
                    DefaultValue = t.DefaultValue,
                    OptionListKey = t.OptionList != null ? t.OptionList.Name : null
                })
                .ToListAsync();

            // Export Notifications with user filtering (via Tag)
            var notificationsQuery = _context.Notifications
                .Include(n => n.Tag)
                .AsQueryable();
            
            if (userId.HasValue)
            {
                notificationsQuery = notificationsQuery.Where(n => n.Tag.UserId == userId);
            }

            var notifications = await notificationsQuery
                .Select(n => new NotificationBackup
                {
                    TagKey = n.Tag.TagName,
                    NotificationText = n.NotificationText,
                    NotBeforeTime = n.NotBeforeTime,
                    NotAfterTime = n.NotAfterTime,
                    MaxNudges = n.MaxNudges,
                    NudgeInterval = n.NudgeInterval,
                    IsActive = n.IsActive,
                    DateCreated = n.DateCreated,
                    LastDeliveryDate = n.LastDeliveryDate,
                    DeliveriesOnLastDate = n.DeliveriesOnLastDate
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
                    Version = "1.2",
                    TotalInputTypes = inputTypes.Count,
                    TotalPatterns = patterns.Count,
                    TotalUnits = units.Count,
                    TotalTagOptionLists = tagOptionLists.Count,
                    TotalTagOptions = tagOptions.Count,
                    TotalTags = tags.Count,
                    TotalNotifications = notifications.Count,
                    TotalActivities = activities.Count
                },
                InputTypes = inputTypes,
                Patterns = patterns,
                Units = units,
                TagOptionLists = tagOptionLists,
                TagOptions = tagOptions,
                Tags = tags,
                Notifications = notifications,
                Activities = activities
            };

            _logger.LogInformation("Data export completed. Tags: {TagCount}, Activities: {ActivityCount}, InputTypes: {InputTypeCount}, Patterns: {PatternCount}, Units: {UnitCount}, TagOptionLists: {TagOptionListCount}, TagOptions: {TagOptionCount}, Notifications: {NotificationCount}",
                tags.Count, activities.Count, inputTypes.Count, patterns.Count, units.Count, tagOptionLists.Count, tagOptions.Count, notifications.Count);

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

                // Import in correct dependency order:
                // InputTypes -> Patterns -> Units -> TagOptionLists -> TagOptions -> Tags -> Notifications -> Activities
                await ImportInputTypesAsync(backupData.InputTypes, result);
                await ImportPatternsAsync(backupData.Patterns, result);
                await ImportUnitsAsync(backupData.Units, result);
                await ImportTagOptionListsAsync(backupData.TagOptionLists, result, userId);
                await ImportTagOptionsAsync(backupData.TagOptions, result);
                await ImportTagsAsync(backupData.Tags, result, userId);
                await ImportNotificationsAsync(backupData.Notifications, result, userId);
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
            // Clear in reverse dependency order to avoid foreign key constraint violations
            
            // 1. Clear activities first (depends on tags)
            var activitiesQuery = _context.Activities.AsQueryable();
            if (userId.HasValue)
            {
                activitiesQuery = activitiesQuery.Where(a => a.UserId == userId);
            }
            var activitiesToDelete = await activitiesQuery.ToListAsync();
            _context.Activities.RemoveRange(activitiesToDelete);
            recordsCleared += activitiesToDelete.Count;

            // 2. Clear notifications (depends on tags)
            var notificationsQuery = _context.Notifications
                .Include(n => n.Tag)
                .AsQueryable();
            if (userId.HasValue)
            {
                notificationsQuery = notificationsQuery.Where(n => n.Tag.UserId == userId);
            }
            var notificationsToDelete = await notificationsQuery.ToListAsync();
            _context.Notifications.RemoveRange(notificationsToDelete);
            recordsCleared += notificationsToDelete.Count;

            // 3. Clear tags (depends on units and option lists)
            var tagsQuery = _context.Tags.AsQueryable();
            if (userId.HasValue)
            {
                tagsQuery = tagsQuery.Where(t => t.UserId == userId);
            }
            var tagsToDelete = await tagsQuery.ToListAsync();
            _context.Tags.RemoveRange(tagsToDelete);
            recordsCleared += tagsToDelete.Count;

            // 4. Clear tag options (depends on tag option lists)
            var tagOptionsQuery = _context.TagOptions
                .Include(to => to.OptionList)
                .AsQueryable();
            if (userId.HasValue)
            {
                tagOptionsQuery = tagOptionsQuery.Where(to => to.OptionList != null && to.OptionList.UserId == userId);
            }
            var tagOptionsToDelete = await tagOptionsQuery.ToListAsync();
            _context.TagOptions.RemoveRange(tagOptionsToDelete);
            recordsCleared += tagOptionsToDelete.Count;

            // 5. Clear tag option lists
            var tagOptionListsQuery = _context.TagOptionLists.AsQueryable();
            if (userId.HasValue)
            {
                tagOptionListsQuery = tagOptionListsQuery.Where(ol => ol.UserId == userId);
            }
            var tagOptionListsToDelete = await tagOptionListsQuery.ToListAsync();
            _context.TagOptionLists.RemoveRange(tagOptionListsToDelete);
            recordsCleared += tagOptionListsToDelete.Count;

            // Clear patterns and input types only if clearing all data (no user filter)
            // Units are global and not user-specific, so they're never cleared
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

        // Validate Unit references in tags
        var unitKeys = backupData.Units.Select(u => u.Key).ToHashSet();
        var invalidTagUnits = backupData.Tags
            .Where(t => !string.IsNullOrEmpty(t.UnitKey) && !unitKeys.Contains(t.UnitKey))
            .ToList();

        if (invalidTagUnits.Any())
        {
            result.Warnings.Add($"Found {invalidTagUnits.Count} tags referencing non-existent units (will be skipped)");
        }

        // Validate TagOptionList references in tags
        var tagOptionListNames = backupData.TagOptionLists.Select(ol => ol.Name).ToHashSet();
        var invalidTagOptionLists = backupData.Tags
            .Where(t => !string.IsNullOrEmpty(t.OptionListKey) && !tagOptionListNames.Contains(t.OptionListKey))
            .ToList();

        if (invalidTagOptionLists.Any())
        {
            result.Warnings.Add($"Found {invalidTagOptionLists.Count} tags referencing non-existent option lists (will be skipped)");
        }

        // Validate TagOptions reference existing TagOptionLists
        var invalidTagOptions = backupData.TagOptions
            .Where(to => !tagOptionListNames.Contains(to.TagOptionListKey))
            .ToList();

        if (invalidTagOptions.Any())
        {
            result.Warnings.Add($"Found {invalidTagOptions.Count} tag options referencing non-existent option lists (will be skipped)");
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

    private async Task ImportUnitsAsync(List<UnitBackup> units, BackupImportResult result)
    {
        var existingUnits = await _context.Units
            .Select(u => u.Key)
            .ToHashSetAsync();

        // Get quantity lookup for foreign key resolution
        var quantityLookup = await _context.Set<Quantity>()
            .ToDictionaryAsync(q => q.Key, q => q.Id);

        foreach (var unit in units)
        {
            if (existingUnits.Contains(unit.Key))
            {
                result.Statistics.UnitsSkipped++;
                continue;
            }

            var entity = new Unit
            {
                Key = unit.Key,
                Symbol = unit.Symbol,
                AToBase = unit.AToBase,
                BToBase = unit.BToBase,
                Decimals = unit.Decimals,
                QuantityId = !string.IsNullOrEmpty(unit.QuantityKey) && quantityLookup.ContainsKey(unit.QuantityKey)
                    ? quantityLookup[unit.QuantityKey]
                    : 0
            };

            _context.Units.Add(entity);
            result.Statistics.UnitsImported++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task ImportTagOptionListsAsync(List<TagOptionListBackup> tagOptionLists, BackupImportResult result, Guid? userId)
    {
        var existingTagOptionLists = await _context.TagOptionLists
            .Where(ol => userId == null || ol.UserId == userId)
            .Select(ol => ol.Name)
            .ToHashSetAsync();

        foreach (var list in tagOptionLists)
        {
            if (existingTagOptionLists.Contains(list.Name))
            {
                result.Statistics.TagOptionListsSkipped++;
                continue;
            }

            var entity = new TagOptionList
            {
                Name = list.Name,
                UserId = userId ?? list.UserId
            };

            _context.TagOptionLists.Add(entity);
            result.Statistics.TagOptionListsImported++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task ImportTagOptionsAsync(List<TagOptionBackup> tagOptions, BackupImportResult result)
    {
        // Get tag option list lookup
        var tagOptionListLookup = await _context.TagOptionLists
            .ToDictionaryAsync(ol => ol.Name, ol => ol.Id);

        foreach (var option in tagOptions)
        {
            if (string.IsNullOrEmpty(option.TagOptionListKey) || !tagOptionListLookup.ContainsKey(option.TagOptionListKey))
            {
                result.Warnings.Add($"Skipping tag option '{option.Value}' - unknown list '{option.TagOptionListKey}'");
                result.Statistics.TagOptionsSkipped++;
                continue;
            }

            var entity = new TagOption
            {
                Value = option.Value,
                DisplayName = option.DisplayName,
                OptionListId = tagOptionListLookup[option.TagOptionListKey]
            };

            _context.TagOptions.Add(entity);
            result.Statistics.TagOptionsImported++;
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

        var unitLookup = await _context.Units
            .ToDictionaryAsync(u => u.Key, u => u.Id);

        var tagOptionListLookup = await _context.TagOptionLists
            .Where(ol => userId == null || ol.UserId == userId)
            .ToDictionaryAsync(ol => ol.Name, ol => ol.Id);

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
                UnitId = !string.IsNullOrEmpty(tag.UnitKey) && unitLookup.ContainsKey(tag.UnitKey)
                    ? unitLookup[tag.UnitKey] : null,
                MinValue = tag.MinValue,
                MaxValue = tag.MaxValue,
                Step = tag.Step,
                DefaultValue = tag.DefaultValue,
                OptionListId = !string.IsNullOrEmpty(tag.OptionListKey) && tagOptionListLookup.ContainsKey(tag.OptionListKey)
                    ? tagOptionListLookup[tag.OptionListKey] : null,
                UserId = userId ?? tag.UserId
            };

            _context.Tags.Add(entity);
            result.Statistics.TagsImported++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task ImportNotificationsAsync(List<NotificationBackup> notifications, BackupImportResult result, Guid? userId)
    {
        // Get tag lookup
        var tagLookup = await _context.Tags
            .Where(t => userId == null || t.UserId == userId)
            .ToDictionaryAsync(t => t.TagName, t => t.Id);

        foreach (var notification in notifications)
        {
            if (string.IsNullOrEmpty(notification.TagKey) || !tagLookup.ContainsKey(notification.TagKey))
            {
                result.Warnings.Add($"Skipping notification for unknown tag '{notification.TagKey}'");
                result.Statistics.NotificationsSkipped++;
                continue;
            }

            // Check if notification already exists for this tag
            var tagId = tagLookup[notification.TagKey];
            var existingNotification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.TagId == tagId);

            if (existingNotification != null)
            {
                result.Statistics.NotificationsSkipped++;
                continue;
            }

            var entity = new Notification
            {
                TagId = tagId,
                NotificationText = notification.NotificationText,
                NotBeforeTime = notification.NotBeforeTime,
                NotAfterTime = notification.NotAfterTime,
                MaxNudges = notification.MaxNudges,
                NudgeInterval = notification.NudgeInterval,
                IsActive = notification.IsActive,
                DateCreated = notification.DateCreated,
                LastDeliveryDate = notification.LastDeliveryDate,
                DeliveriesOnLastDate = notification.DeliveriesOnLastDate
            };

            _context.Notifications.Add(entity);
            result.Statistics.NotificationsImported++;
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

    // NEW: Secure user-scoped backup methods (v2.0)
    
    /// <summary>
    /// Creates a secure backup of the current user's data (activities and tags only, no user credentials)
    /// </summary>
    public async Task<SecureBackupDto> CreateSecureBackupAsync(Guid userId)
    {
        _logger.LogInformation("Creating secure backup for user: {UserId}", userId);

        try
        {
            // Export user's tags (excluding sensitive data, user IDs)
            var userTags = await _context.Tags
                .Include(t => t.InputType)
                .Include(t => t.Pattern)
                .Where(t => t.UserId == userId)
                .Select(t => new SecureTagBackupDto
                {
                    Id = t.Id,
                    TagName = t.TagName,
                    InputTypeName = t.InputType != null ? t.InputType.Name : null,
                    IsRequired = t.IsRequired,
                    TimeGranularity = t.TimeGranularity.ToString(),
                    IsRepeatable = t.IsRepeatable,
                    IsRange = t.IsRange,
                    PatternName = t.Pattern != null ? t.Pattern.Name : null
                    // Note: UserId explicitly excluded for security
                })
                .ToListAsync();

            // Export user's activities (excluding sensitive data, user IDs)
            var userActivities = await _context.Activities
                .Include(a => a.Tag)
                .Where(a => a.UserId == userId)
                .Select(a => new SecureActivityBackupDto
                {
                    Id = a.Id,
                    Description = a.Description,
                    DateCreated = a.DateCreated,
                    DateStarted = a.DateStarted,
                    DateFinished = a.DateFinished,
                    TagName = a.Tag.TagName // For restoration matching
                    // Note: UserId explicitly excluded for security
                })
                .ToListAsync();

            var secureBackup = new SecureBackupDto
            {
                CreatedAt = DateTime.UtcNow,
                Version = "2.0", // New secure backup format version
                Activities = userActivities,
                Tags = userTags
                // Note: Explicitly NO user data, credentials, or sensitive information
            };

            _logger.LogInformation("Secure backup created successfully. Tags: {TagCount}, Activities: {ActivityCount}", 
                userTags.Count, userActivities.Count);

            return secureBackup;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during secure backup creation for user: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Restores data from secure backup and assigns it to the specified user
    /// </summary>
    public async Task<BackupImportResult> RestoreSecureBackupAsync(SecureBackupDto backup, Guid userId)
    {
        _logger.LogInformation("🚀 STARTING secure backup restore for user: {UserId}", userId);
        _logger.LogInformation("� DEBUG: UserId parameter type: {Type}, Value: {Value}, IsEmpty: {IsEmpty}", 
            userId.GetType().Name, userId, userId == Guid.Empty);
        _logger.LogInformation("�📦 Backup contains {TagCount} tags and {ActivityCount} activities", backup.Tags.Count, backup.Activities.Count);
        
        var result = new BackupImportResult { Success = true };
        
        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Step 1: Import tags with current user ID
                foreach (var tagDto in backup.Tags)
                {
                    // Check if tag already exists for this user
                    var existingTag = await _context.Tags
                        .FirstOrDefaultAsync(t => t.TagName == tagDto.TagName && t.UserId == userId);
                    
                    if (existingTag == null)
                    {
                        // Get input type if specified
                        int? inputTypeId = null;
                        if (!string.IsNullOrEmpty(tagDto.InputTypeName))
                        {
                            var inputType = await _context.InputTypes
                                .FirstOrDefaultAsync(it => it.Name == tagDto.InputTypeName);
                            inputTypeId = inputType?.Id;
                        }

                        // Get pattern if specified
                        int? patternId = null;
                        if (!string.IsNullOrEmpty(tagDto.PatternName))
                        {
                            var pattern = await _context.Patterns
                                .FirstOrDefaultAsync(p => p.Name == tagDto.PatternName);
                            patternId = pattern?.Id;
                        }

                        // Parse TimeGranularity enum
                        var timeGranularity = Domain.Enums.TimeGranularity.Exact;
                        if (!string.IsNullOrEmpty(tagDto.TimeGranularity))
                        {
                            Enum.TryParse<Domain.Enums.TimeGranularity>(tagDto.TimeGranularity, out timeGranularity);
                        }

                        // Create new tag - SIMPLE VERSION
                        var newTag = new Tag
                        {
                            TagName = tagDto.TagName,
                            InputTypeId = inputTypeId,
                            IsRequired = tagDto.IsRequired,
                            TimeGranularity = timeGranularity,
                            IsRepeatable = tagDto.IsRepeatable,
                            IsRange = tagDto.IsRange,
                            PatternId = patternId,
                            UserId = userId  // Simple assignment
                        };
                        
                        _context.Tags.Add(newTag);
                        result.Statistics.TagsImported++;
                        
                        _logger.LogInformation("✅ Adding tag '{TagName}' for user {UserId}", tagDto.TagName, userId);
                    }
                    else
                    {
                        result.Statistics.TagsSkipped++;
                    }
                }

                // Save tags first
                await _context.SaveChangesAsync();

                // Step 2: Import activities with current user ID
                foreach (var activityDto in backup.Activities)
                {
                    // Find the tag by name in the current user's tags
                    var tag = await _context.Tags
                        .FirstOrDefaultAsync(t => t.TagName == activityDto.TagName && t.UserId == userId);
                    
                    if (tag != null)
                    {
                        // Create new activity - SIMPLE VERSION
                        var newActivity = new Activity
                        {
                            Description = activityDto.Description,
                            DateCreated = activityDto.DateCreated,
                            DateStarted = activityDto.DateStarted,
                            DateFinished = activityDto.DateFinished,
                            TagId = tag.Id,
                            UserId = userId  // Simple assignment
                        };
                        
                        _context.Activities.Add(newActivity);
                        result.Statistics.ActivitiesImported++;
                        
                        _logger.LogInformation("✅ Adding activity '{Description}' for user {UserId}", activityDto.Description, userId);
                    }
                    else
                    {
                        result.Warnings.Add($"Skipping activity with unknown tag: {activityDto.TagName}");
                        result.Statistics.ActivitiesSkipped++;
                    }
                }

                // Save activities
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                result.Message = "Secure backup restored successfully. All data has been assigned to your user account.";
                
                _logger.LogInformation("✅ Secure backup restore completed successfully for user: {UserId}. Tags: {TagsImported}, Activities: {ActivitiesImported}", 
                    userId, result.Statistics.TagsImported, result.Statistics.ActivitiesImported);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Secure backup restore failed: {ex.Message}";
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "Secure backup restore failed for user: {UserId}", userId);
        }

        return result;
    }

    /// <summary>
    /// Clears all data for the specified user only (preserves other users' data)
    /// </summary>
    public async Task<int> ClearUserDataAsync(Guid userId)
    {
        _logger.LogInformation("Clearing all data for user: {UserId}", userId);

        int recordsCleared = 0;

        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // 🎯 ONLY clear specified user's data - NOT all users!
                
                // Clear user's activities first (due to foreign key constraints)
                var userActivities = await _context.Activities
                    .Where(a => a.UserId == userId)
                    .ToListAsync();
                
                _context.Activities.RemoveRange(userActivities);
                recordsCleared += userActivities.Count;

                // Clear user's tags
                var userTags = await _context.Tags
                    .Where(t => t.UserId == userId)
                    .ToListAsync();
                
                _context.Tags.RemoveRange(userTags);
                recordsCleared += userTags.Count;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Cleared {RecordsCleared} records for user: {UserId}", recordsCleared, userId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during user data clearing for user: {UserId}", userId);
            throw;
        }

        return recordsCleared;
    }
}
