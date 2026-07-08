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
        _logger.LogInformation("Starting data export");

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
                    Name = ol.Name
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
                .Include(t => t.Group)
                .AsQueryable();

            if (userId.HasValue)
            {
                tagsQuery = tagsQuery.Where(t => t.UserId == userId);
            }

            var tags = await tagsQuery
                .Select(t => new TagBackup
                {
                    TagName = t.TagName,
                    Description = t.Description,
                    InputTypeName = t.InputType != null ? t.InputType.Name : null,
                    IsRequired = t.IsRequired,
                    TimeGranularity = t.TimeGranularity,
                    IsRepeatable = t.IsRepeatable,
                    IsRange = t.IsRange,
                    PatternName = t.Pattern != null ? t.Pattern.Name : null,
                    UnitKey = t.Unit != null ? t.Unit.Key : null,
                    UnitSymbol = t.Unit != null ? t.Unit.Symbol : null,
                    MinValue = t.MinValue,
                    MaxValue = t.MaxValue,
                    Step = t.Step,
                    DefaultValue = t.DefaultValue,
                    OptionListKey = t.OptionList != null ? t.OptionList.Name : null,
                    GroupName = t.Group != null ? t.Group.Name : null
                })
                .ToListAsync();

            // Export TagGroups with user filtering
            var tagGroupsQuery = _context.TagGroups.AsQueryable();
            if (userId.HasValue)
            {
                tagGroupsQuery = tagGroupsQuery.Where(g => g.UserId == userId);
            }

            var tagGroups = await tagGroupsQuery
                .Select(g => new TagGroupBackup
                {
                    Name = g.Name,
                    Description = g.Description,
                    DisplayOrder = g.DisplayOrder,
                    DateCreated = g.DateCreated
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
                .OrderBy(a => a.Tag.TagName)
                .ThenBy(a => a.DateStarted)
                .Select(a => new ActivityBackup
                {
                    DateCreated = a.DateCreated,
                    DateStarted = a.DateStarted,
                    DateFinished = a.DateFinished,
                    Description = a.Description,
                    TagName = a.Tag.TagName
                })
                .ToListAsync();

            var totalActivities = activities.Count;
            activities = CompressActivitiesToStreaks(activities);

            // Export ScanMappings with user filtering
            var scanMappingsQuery = _context.ScanMappings
                .Include(sm => sm.Tag)
                .AsQueryable();

            if (userId.HasValue)
            {
                scanMappingsQuery = scanMappingsQuery.Where(sm => sm.UserId == userId);
            }

            var scanMappings = await scanMappingsQuery
                .Select(sm => new ScanMappingBackup
                {
                    CodeValue = sm.CodeValue,
                    CodeType = (int)sm.CodeType,
                    TagName = sm.Tag.TagName,
                    DisplayName = sm.DisplayName,
                    DefaultDescription = sm.DefaultDescription,
                    IsActive = sm.IsActive,
                    DateCreated = sm.DateCreated
                })
                .ToListAsync();

            // Export TodoLists with items and user filtering
            var todoListsQuery = _context.TodoLists
                .Include(l => l.Items)
                    .ThenInclude(i => i.CompletionTag)
                .AsQueryable();

            if (userId.HasValue)
            {
                todoListsQuery = todoListsQuery.Where(l => l.UserId == userId);
            }

            var todoLists = await todoListsQuery
                .Select(l => new TodoListBackup
                {
                    Name = l.Name,
                    DisplayOrder = l.DisplayOrder,
                    ShowOnHomepage = l.ShowOnHomepage,
                    DateCreated = l.DateCreated,
                    Items = l.Items.Select(i => new TodoItemBackup
                    {
                        Title = i.Title,
                        Notes = i.Notes,
                        StartDate = i.StartDate,
                        DueDate = i.DueDate,
                        NotifyAt = i.NotifyAt,
                        IsDone = i.IsDone,
                        DoneAt = i.DoneAt,
                        SkippedAt = i.SkippedAt,
                        DisplayOrder = i.DisplayOrder,
                        DateCreated = i.DateCreated,
                        RecurrenceType = i.RecurrenceType,
                        AutoLogMode = i.AutoLogMode,
                        CompletionTagName = i.CompletionTag != null ? i.CompletionTag.TagName : null
                    }).ToList()
                })
                .ToListAsync();

            // Reminders are flat (UserId-scoped) — no list container.
            var remindersQuery = _context.Reminders
                .Include(i => i.CompletionTag)
                .AsQueryable();

            if (userId.HasValue)
            {
                remindersQuery = remindersQuery.Where(i => i.UserId == userId);
            }

            var reminders = await remindersQuery
                .Select(i => new ReminderBackup
                {
                    Title = i.Title,
                    Notes = i.Notes,
                    NotifyAt = i.NotifyAt,
                    IsDone = i.IsDone,
                    DoneAt = i.DoneAt,
                    SkippedAt = i.SkippedAt,
                    DisplayOrder = i.DisplayOrder,
                    DateCreated = i.DateCreated,
                    RecurrenceType = i.RecurrenceType,
                    AutoLogMode = i.AutoLogMode,
                    CompletionTagName = i.CompletionTag != null ? i.CompletionTag.TagName : null,
                    MonitorFromDate = i.MonitorFromDate,
                    MonitorToDate = i.MonitorToDate,
                    AllowUnfilled = i.AllowUnfilled,
                    Days = i.Days.Select(d => new ReminderDayBackup
                    {
                        Date = d.Date,
                        IsDone = d.IsDone,
                        DoneAt = d.DoneAt,
                        IsSkipped = d.IsSkipped,
                        SkippedAt = d.SkippedAt,
                        CompletionValue = d.CompletionValue
                    }).ToList()
                })
                .ToListAsync();

            var backupData = new BackupData
            {
                Metadata = new BackupMetadata
                {
                    ExportDate = DateTime.UtcNow,
                    Version = "2.0",
                    TotalInputTypes = inputTypes.Count,
                    TotalPatterns = patterns.Count,
                    TotalUnits = units.Count,
                    TotalTagOptionLists = tagOptionLists.Count,
                    TotalTagOptions = tagOptions.Count,
                    TotalTagGroups = tagGroups.Count,
                    TotalTags = tags.Count,
                    TotalActivities = totalActivities,
                    TotalScanMappings = scanMappings.Count,
                    TotalTodoLists = todoLists.Count,
                    TotalTodoItems = todoLists.Sum(l => l.Items.Count),
                    TotalReminders = reminders.Count
                },
                InputTypes = inputTypes,
                Patterns = patterns,
                Units = units,
                TagOptionLists = tagOptionLists,
                TagOptions = tagOptions,
                TagGroups = tagGroups,
                Tags = tags,
                Activities = activities,
                ScanMappings = scanMappings,
                TodoLists = todoLists,
                Reminders = reminders
            };

            _logger.LogInformation("Data export completed successfully");

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
        _logger.LogInformation("Starting data import");

        var result = new BackupImportResult { Success = true };

        try
        {
            // Validate backup data first
            var validation = await ValidateBackupData(backupData);
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
                // InputTypes -> Patterns -> Units -> TagOptionLists -> TagOptions -> TagGroups -> Tags -> Activities
                await ImportInputTypesAsync(backupData.InputTypes, result);
                await ImportPatternsAsync(backupData.Patterns, result);
                await ImportUnitsAsync(backupData.Units, result);
                await ImportTagOptionListsAsync(backupData.TagOptionLists, result, userId);
                await ImportTagOptionsAsync(backupData.TagOptions, result);
                await ImportTagGroupsAsync(backupData.TagGroups, result, userId);
                await ImportTagsAsync(backupData.Tags, result, userId);
                await ImportActivitiesAsync(backupData.Activities, result, userId);
                await ImportScanMappingsAsync(backupData.ScanMappings, result, userId);
                await ImportTodoListsAsync(backupData.TodoLists, result, userId);
                await ImportRemindersAsync(backupData.Reminders, result, userId);

                await transaction.CommitAsync();
                result.Message = "Data import completed successfully";

                _logger.LogInformation("Data import completed successfully");
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
        _logger.LogInformation("Clearing data");

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

            // 2. Clear scan mappings (depends on tags)
            var scanMappingsQuery = _context.ScanMappings.AsQueryable();
            if (userId.HasValue)
            {
                scanMappingsQuery = scanMappingsQuery.Where(sm => sm.UserId == userId);
            }
            var scanMappingsToDelete = await scanMappingsQuery.ToListAsync();
            _context.ScanMappings.RemoveRange(scanMappingsToDelete);
            recordsCleared += scanMappingsToDelete.Count;

            // 3. Clear todo items (depends on todo lists)
            var todoItemsQuery = _context.TodoItems
                .Include(i => i.List)
                .AsQueryable();
            if (userId.HasValue)
            {
                todoItemsQuery = todoItemsQuery.Where(i => i.List.UserId == userId);
            }
            var todoItemsToDelete = await todoItemsQuery.ToListAsync();
            _context.TodoItems.RemoveRange(todoItemsToDelete);
            recordsCleared += todoItemsToDelete.Count;

            // 4. Clear todo lists
            var todoListsQuery = _context.TodoLists.AsQueryable();
            if (userId.HasValue)
            {
                todoListsQuery = todoListsQuery.Where(l => l.UserId == userId);
            }
            var todoListsToDelete = await todoListsQuery.ToListAsync();
            _context.TodoLists.RemoveRange(todoListsToDelete);
            recordsCleared += todoListsToDelete.Count;

            // 5. Clear tags (depends on units and option lists)
            var tagsQuery = _context.Tags.AsQueryable();
            if (userId.HasValue)
            {
                tagsQuery = tagsQuery.Where(t => t.UserId == userId);
            }
            var tagsToDelete = await tagsQuery.ToListAsync();
            _context.Tags.RemoveRange(tagsToDelete);
            recordsCleared += tagsToDelete.Count;

            // 7. Clear tag groups
            var tagGroupsQuery = _context.TagGroups.AsQueryable();
            if (userId.HasValue)
            {
                tagGroupsQuery = tagGroupsQuery.Where(g => g.UserId == userId);
            }
            var tagGroupsToDelete = await tagGroupsQuery.ToListAsync();
            _context.TagGroups.RemoveRange(tagGroupsToDelete);
            recordsCleared += tagGroupsToDelete.Count;

            // 8. Clear tag options (depends on tag option lists)
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

            // 9. Clear tag option lists
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

            _logger.LogInformation("Data cleared successfully");
            return recordsCleared;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during data clearing");
            throw;
        }
    }

    public Task<BackupValidationResult> ValidateBackupData(BackupData backupData)
    {
        var result = new BackupValidationResult { IsValid = true };

        if (backupData == null)
        {
            result.IsValid = false;
            result.Errors.Add("Backup data is null");
            return Task.FromResult(result);
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

        // Validate streak dates
        var invalidStreaks = backupData.Activities
            .Where(a => a.StreakEndDate.HasValue && a.StreakEndDate.Value.Date < a.DateStarted.Date)
            .ToList();

        if (invalidStreaks.Any())
        {
            result.Errors.Add($"Found {invalidStreaks.Count} activities with StreakEndDate before DateStarted");
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

        // Validate ScanMapping references
        var invalidScanMappings = backupData.ScanMappings
            .Where(sm => !string.IsNullOrEmpty(sm.TagName) && !tagNames.Contains(sm.TagName))
            .ToList();

        if (invalidScanMappings.Any())
        {
            result.Errors.Add($"Found {invalidScanMappings.Count} scan mappings referencing non-existent tags");
            result.IsValid = false;
        }

        // Validate ScanMapping duplicate CodeValues
        var duplicateCodeValues = backupData.ScanMappings
            .GroupBy(sm => sm.CodeValue)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateCodeValues.Any())
        {
            result.Errors.Add($"Duplicate scan mapping code values found: {string.Join(", ", duplicateCodeValues)}");
            result.IsValid = false;
        }

        return Task.FromResult(result);
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
                UserId = userId
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

    private async Task ImportTagGroupsAsync(List<TagGroupBackup> tagGroups, BackupImportResult result, Guid? userId)
    {
        var existingGroupNames = await _context.TagGroups
            .Where(g => userId == null || g.UserId == userId)
            .Select(g => g.Name)
            .ToHashSetAsync();

        foreach (var group in tagGroups)
        {
            if (existingGroupNames.Contains(group.Name))
            {
                result.Statistics.TagGroupsSkipped++;
                continue;
            }

            var entity = new TagGroup
            {
                Name = group.Name,
                UserId = userId ?? Guid.Empty,
                Description = group.Description,
                DisplayOrder = group.DisplayOrder,
                DateCreated = group.DateCreated
            };

            _context.TagGroups.Add(entity);
            result.Statistics.TagGroupsImported++;
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

        var tagGroupLookup = await _context.TagGroups
            .Where(g => userId == null || g.UserId == userId)
            .ToDictionaryAsync(g => g.Name, g => g.Id);

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
                Description = tag.Description,
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
                GroupId = !string.IsNullOrEmpty(tag.GroupName) && tagGroupLookup.ContainsKey(tag.GroupName)
                    ? tagGroupLookup[tag.GroupName] : null,
                UserId = userId ?? Guid.Empty
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

            var tagId = tagLookup[activity.TagName];
            var resolvedUserId = userId ?? Guid.Empty;

            if (activity.StreakEndDate.HasValue)
            {
                var startDate = activity.DateStarted.Date;
                var endDate = activity.StreakEndDate.Value.Date;

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    var entity = new Activity
                    {
                        DateCreated = activity.DateCreated,
                        DateStarted = date.Add(activity.DateStarted.TimeOfDay),
                        DateFinished = activity.DateFinished,
                        Description = activity.Description,
                        TagId = tagId,
                        UserId = resolvedUserId
                    };

                    _context.Activities.Add(entity);
                    result.Statistics.ActivitiesImported++;
                }
            }
            else
            {
                var entity = new Activity
                {
                    DateCreated = activity.DateCreated,
                    DateStarted = activity.DateStarted,
                    DateFinished = activity.DateFinished,
                    Description = activity.Description,
                    TagId = tagId,
                    UserId = resolvedUserId
                };

                _context.Activities.Add(entity);
                result.Statistics.ActivitiesImported++;
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task ImportScanMappingsAsync(List<ScanMappingBackup> scanMappings, BackupImportResult result, Guid? userId)
    {
        var tagLookup = await _context.Tags
            .Where(t => userId == null || t.UserId == userId)
            .ToDictionaryAsync(t => t.TagName, t => t.Id);

        var existingCodeValues = await _context.ScanMappings
            .Where(sm => userId == null || sm.UserId == userId)
            .Select(sm => sm.CodeValue)
            .ToHashSetAsync();

        foreach (var mapping in scanMappings)
        {
            if (existingCodeValues.Contains(mapping.CodeValue))
            {
                result.Statistics.ScanMappingsSkipped++;
                continue;
            }

            if (string.IsNullOrEmpty(mapping.TagName) || !tagLookup.ContainsKey(mapping.TagName))
            {
                result.Warnings.Add($"Skipping scan mapping '{mapping.CodeValue}' - unknown tag '{mapping.TagName}'");
                result.Statistics.ScanMappingsSkipped++;
                continue;
            }

            var entity = new ScanMapping
            {
                CodeValue = mapping.CodeValue,
                CodeType = (Domain.Enums.CodeType)mapping.CodeType,
                TagId = tagLookup[mapping.TagName],
                UserId = userId ?? Guid.Empty,
                DisplayName = mapping.DisplayName,
                DefaultDescription = mapping.DefaultDescription,
                IsActive = mapping.IsActive,
                DateCreated = mapping.DateCreated
            };

            _context.ScanMappings.Add(entity);
            result.Statistics.ScanMappingsImported++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task ImportTodoListsAsync(List<TodoListBackup> todoLists, BackupImportResult result, Guid? userId)
    {
        var tagLookup = await _context.Tags
            .Where(t => userId == null || t.UserId == userId)
            .ToDictionaryAsync(t => t.TagName, t => t.Id);

        var existingListNames = await _context.TodoLists
            .Where(l => userId == null || l.UserId == userId)
            .Select(l => l.Name)
            .ToHashSetAsync();

        foreach (var list in todoLists)
        {
            if (existingListNames.Contains(list.Name))
            {
                result.Statistics.TodoListsSkipped++;
                result.Statistics.TodoItemsSkipped += list.Items.Count;
                continue;
            }

            var listEntity = new TodoList
            {
                Name = list.Name,
                DisplayOrder = list.DisplayOrder,
                ShowOnHomepage = list.ShowOnHomepage,
                DateCreated = list.DateCreated,
                UserId = userId ?? Guid.Empty
            };

            _context.TodoLists.Add(listEntity);
            await _context.SaveChangesAsync();
            result.Statistics.TodoListsImported++;

            foreach (var item in list.Items)
            {
                int? completionTagId = null;
                if (!string.IsNullOrEmpty(item.CompletionTagName) && tagLookup.ContainsKey(item.CompletionTagName))
                {
                    completionTagId = tagLookup[item.CompletionTagName];
                }

                var itemEntity = new TodoItem
                {
                    ListId = listEntity.Id,
                    Title = item.Title,
                    Notes = item.Notes,
                    StartDate = item.StartDate,
                    DueDate = item.DueDate,
                    NotifyAt = item.NotifyAt,
                    IsDone = item.IsDone,
                    DoneAt = item.DoneAt,
                    SkippedAt = item.SkippedAt,
                    DisplayOrder = item.DisplayOrder,
                    DateCreated = item.DateCreated,
                    RecurrenceType = item.RecurrenceType,
                    AutoLogMode = item.AutoLogMode,
                    CompletionTagId = completionTagId
                };

                _context.TodoItems.Add(itemEntity);
                result.Statistics.TodoItemsImported++;
            }

            await _context.SaveChangesAsync();
        }
    }

    private async Task ImportRemindersAsync(List<ReminderBackup> reminders, BackupImportResult result, Guid? userId)
    {
        var tagLookup = await _context.Tags
            .Where(t => userId == null || t.UserId == userId)
            .ToDictionaryAsync(t => t.TagName, t => t.Id);

        var existingTitles = await _context.Reminders
            .Where(r => userId == null || r.UserId == userId)
            .Select(r => r.Title)
            .ToHashSetAsync();

        foreach (var item in reminders)
        {
            if (existingTitles.Contains(item.Title))
            {
                result.Statistics.RemindersSkipped++;
                continue;
            }

            int? completionTagId = null;
            if (!string.IsNullOrEmpty(item.CompletionTagName) && tagLookup.ContainsKey(item.CompletionTagName))
            {
                completionTagId = tagLookup[item.CompletionTagName];
            }

            var itemEntity = new Reminder
            {
                UserId = userId ?? Guid.Empty,
                Title = item.Title,
                Notes = item.Notes,
                NotifyAt = item.NotifyAt,
                IsDone = item.IsDone,
                DoneAt = item.DoneAt,
                SkippedAt = item.SkippedAt,
                DisplayOrder = item.DisplayOrder,
                DateCreated = item.DateCreated,
                RecurrenceType = item.RecurrenceType,
                AutoLogMode = item.AutoLogMode,
                CompletionTagId = completionTagId,
                MonitorFromDate = item.MonitorFromDate,
                MonitorToDate = item.MonitorToDate,
                AllowUnfilled = item.AllowUnfilled,
                Days = item.Days.Select(d => new ReminderDay
                {
                    UserId = userId ?? Guid.Empty,
                    Date = d.Date,
                    IsDone = d.IsDone,
                    DoneAt = d.DoneAt,
                    IsSkipped = d.IsSkipped,
                    SkippedAt = d.SkippedAt,
                    CompletionValue = d.CompletionValue
                }).ToList()
            };

            _context.Reminders.Add(itemEntity);
            result.Statistics.RemindersImported++;
        }

        await _context.SaveChangesAsync();
    }

    // NEW: Secure user-scoped backup methods (v2.0)
    
    /// <summary>
    /// Creates a secure backup of the current user's data (activities and tags only, no user credentials)
    /// </summary>
    public async Task<SecureBackupDto> CreateSecureBackup(Guid userId)
    {
        _logger.LogInformation("Creating secure backup");

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

            // Export user's tag groups
            var userTagGroups = await _context.TagGroups
                .Where(g => g.UserId == userId)
                .Select(g => new SecureTagGroupBackupDto
                {
                    Name = g.Name,
                    Description = g.Description,
                    DisplayOrder = g.DisplayOrder,
                    DateCreated = g.DateCreated
                })
                .ToListAsync();

            // Export user's tag option lists
            var userTagOptionLists = await _context.TagOptionLists
                .Where(ol => ol.UserId == userId)
                .Select(ol => new SecureTagOptionListBackupDto
                {
                    Name = ol.Name
                })
                .ToListAsync();

            // Export user's tag options (via user's option lists)
            var userTagOptions = await _context.TagOptions
                .Include(to => to.OptionList)
                .Where(to => to.OptionList != null && to.OptionList.UserId == userId)
                .Select(to => new SecureTagOptionBackupDto
                {
                    Value = to.Value,
                    DisplayName = to.DisplayName,
                    TagOptionListKey = to.OptionList != null ? to.OptionList.Name : string.Empty
                })
                .ToListAsync();

            // Export user's scan mappings
            var userScanMappings = await _context.ScanMappings
                .Include(sm => sm.Tag)
                .Where(sm => sm.UserId == userId)
                .Select(sm => new SecureScanMappingBackupDto
                {
                    CodeValue = sm.CodeValue,
                    CodeType = (int)sm.CodeType,
                    TagName = sm.Tag.TagName,
                    DisplayName = sm.DisplayName,
                    DefaultDescription = sm.DefaultDescription,
                    IsActive = sm.IsActive,
                    DateCreated = sm.DateCreated
                })
                .ToListAsync();

            var secureBackup = new SecureBackupDto
            {
                CreatedAt = DateTime.UtcNow,
                Version = "2.1",
                Activities = userActivities,
                Tags = userTags,
                TagGroups = userTagGroups,
                TagOptionLists = userTagOptionLists,
                TagOptions = userTagOptions,
                ScanMappings = userScanMappings
            };

            _logger.LogInformation("Secure backup created successfully");

            return secureBackup;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during secure backup creation");
            throw;
        }
    }

    /// <summary>
    /// Restores data from secure backup and assigns it to the specified user
    /// </summary>
    public async Task<BackupImportResult> RestoreSecureBackup(SecureBackupDto backup, Guid userId)
    {
        _logger.LogInformation("Starting secure backup restore");
        
        var result = new BackupImportResult { Success = true };
        
        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Step 1: Import tag option lists
                foreach (var listDto in backup.TagOptionLists)
                {
                    var exists = await _context.TagOptionLists
                        .AnyAsync(ol => ol.Name == listDto.Name && ol.UserId == userId);

                    if (!exists)
                    {
                        _context.TagOptionLists.Add(new TagOptionList
                        {
                            Name = listDto.Name,
                            UserId = userId
                        });
                        result.Statistics.TagOptionListsImported++;
                    }
                    else
                    {
                        result.Statistics.TagOptionListsSkipped++;
                    }
                }

                await _context.SaveChangesAsync();

                // Step 2: Import tag options
                var optionListLookup = await _context.TagOptionLists
                    .Where(ol => ol.UserId == userId)
                    .ToDictionaryAsync(ol => ol.Name, ol => ol.Id);

                foreach (var optionDto in backup.TagOptions)
                {
                    if (!optionListLookup.TryGetValue(optionDto.TagOptionListKey, out var listId))
                    {
                        result.Statistics.TagOptionsSkipped++;
                        continue;
                    }

                    _context.TagOptions.Add(new TagOption
                    {
                        Value = optionDto.Value,
                        DisplayName = optionDto.DisplayName,
                        OptionListId = listId
                    });
                    result.Statistics.TagOptionsImported++;
                }

                await _context.SaveChangesAsync();

                // Step 3: Import tag groups
                foreach (var groupDto in backup.TagGroups)
                {
                    var exists = await _context.TagGroups
                        .AnyAsync(g => g.Name == groupDto.Name && g.UserId == userId);

                    if (!exists)
                    {
                        _context.TagGroups.Add(new TagGroup
                        {
                            Name = groupDto.Name,
                            UserId = userId,
                            Description = groupDto.Description,
                            DisplayOrder = groupDto.DisplayOrder,
                            DateCreated = groupDto.DateCreated
                        });
                        result.Statistics.TagGroupsImported++;
                    }
                    else
                    {
                        result.Statistics.TagGroupsSkipped++;
                    }
                }

                await _context.SaveChangesAsync();

                // Step 4: Import tags with current user ID
                var tagGroupLookup = await _context.TagGroups
                    .Where(g => g.UserId == userId)
                    .ToDictionaryAsync(g => g.Name, g => g.Id);

                foreach (var tagDto in backup.Tags)
                {
                    var existingTag = await _context.Tags
                        .FirstOrDefaultAsync(t => t.TagName == tagDto.TagName && t.UserId == userId);
                    
                    if (existingTag == null)
                    {
                        int? inputTypeId = null;
                        if (!string.IsNullOrEmpty(tagDto.InputTypeName))
                        {
                            var inputType = await _context.InputTypes
                                .FirstOrDefaultAsync(it => it.Name == tagDto.InputTypeName);
                            inputTypeId = inputType?.Id;
                        }

                        int? patternId = null;
                        if (!string.IsNullOrEmpty(tagDto.PatternName))
                        {
                            var pattern = await _context.Patterns
                                .FirstOrDefaultAsync(p => p.Name == tagDto.PatternName);
                            patternId = pattern?.Id;
                        }

                        var timeGranularity = Domain.Enums.TimeGranularity.Exact;
                        if (!string.IsNullOrEmpty(tagDto.TimeGranularity))
                        {
                            Enum.TryParse<Domain.Enums.TimeGranularity>(tagDto.TimeGranularity, out timeGranularity);
                        }

                        var newTag = new Tag
                        {
                            TagName = tagDto.TagName,
                            InputTypeId = inputTypeId,
                            IsRequired = tagDto.IsRequired,
                            TimeGranularity = timeGranularity,
                            IsRepeatable = tagDto.IsRepeatable,
                            IsRange = tagDto.IsRange,
                            PatternId = patternId,
                            UserId = userId
                        };
                        
                        _context.Tags.Add(newTag);
                        result.Statistics.TagsImported++;
                    }
                    else
                    {
                        result.Statistics.TagsSkipped++;
                    }
                }

                await _context.SaveChangesAsync();

                // Step 5: Import activities with current user ID
                var tagLookup = await _context.Tags
                    .Where(t => t.UserId == userId)
                    .ToDictionaryAsync(t => t.TagName, t => t.Id);

                foreach (var activityDto in backup.Activities)
                {
                    if (!tagLookup.TryGetValue(activityDto.TagName, out var tagId))
                    {
                        result.Warnings.Add($"Skipping activity with unknown tag: {activityDto.TagName}");
                        result.Statistics.ActivitiesSkipped++;
                        continue;
                    }

                    _context.Activities.Add(new Activity
                    {
                        Description = activityDto.Description,
                        DateCreated = activityDto.DateCreated,
                        DateStarted = activityDto.DateStarted,
                        DateFinished = activityDto.DateFinished,
                        TagId = tagId,
                        UserId = userId
                    });
                    result.Statistics.ActivitiesImported++;
                }

                await _context.SaveChangesAsync();

                // Step 7: Import scan mappings
                foreach (var scanDto in backup.ScanMappings)
                {
                    if (!tagLookup.TryGetValue(scanDto.TagName, out var tagId))
                    {
                        result.Warnings.Add($"Skipping scan mapping '{scanDto.CodeValue}' - unknown tag '{scanDto.TagName}'");
                        result.Statistics.ScanMappingsSkipped++;
                        continue;
                    }

                    var exists = await _context.ScanMappings
                        .AnyAsync(sm => sm.CodeValue == scanDto.CodeValue && sm.UserId == userId);

                    if (exists)
                    {
                        result.Statistics.ScanMappingsSkipped++;
                        continue;
                    }

                    _context.ScanMappings.Add(new ScanMapping
                    {
                        CodeValue = scanDto.CodeValue,
                        CodeType = (Domain.Enums.CodeType)scanDto.CodeType,
                        TagId = tagId,
                        UserId = userId,
                        DisplayName = scanDto.DisplayName,
                        DefaultDescription = scanDto.DefaultDescription,
                        IsActive = scanDto.IsActive,
                        DateCreated = scanDto.DateCreated
                    });
                    result.Statistics.ScanMappingsImported++;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                result.Message = "Secure backup restored successfully. All data has been assigned to your user account.";
                
                _logger.LogInformation("Secure backup restore completed successfully");
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
            _logger.LogError(ex, "Secure backup restore failed");
        }

        return result;
    }

    /// <summary>
    /// Clears all data for the specified user only (preserves other users' data)
    /// </summary>
    public async Task<int> ClearUserData(Guid userId)
    {
        _logger.LogInformation("Clearing user data");

        int recordsCleared = 0;

        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Clear in reverse dependency order

                // 1. Clear user's activities
                var userActivities = await _context.Activities
                    .Where(a => a.UserId == userId)
                    .ToListAsync();

                _context.Activities.RemoveRange(userActivities);
                recordsCleared += userActivities.Count;

                // 2. Clear user's scan mappings
                var userScanMappings = await _context.ScanMappings
                    .Where(sm => sm.UserId == userId)
                    .ToListAsync();

                _context.ScanMappings.RemoveRange(userScanMappings);
                recordsCleared += userScanMappings.Count;

                // 3. Clear user's tags
                var userTags = await _context.Tags
                    .Where(t => t.UserId == userId)
                    .ToListAsync();

                _context.Tags.RemoveRange(userTags);
                recordsCleared += userTags.Count;

                // 5. Clear user's tag groups
                var userTagGroups = await _context.TagGroups
                    .Where(g => g.UserId == userId)
                    .ToListAsync();

                _context.TagGroups.RemoveRange(userTagGroups);
                recordsCleared += userTagGroups.Count;

                // 6. Clear user's tag options (via option lists)
                var userTagOptions = await _context.TagOptions
                    .Include(to => to.OptionList)
                    .Where(to => to.OptionList != null && to.OptionList.UserId == userId)
                    .ToListAsync();

                _context.TagOptions.RemoveRange(userTagOptions);
                recordsCleared += userTagOptions.Count;

                // 7. Clear user's tag option lists
                var userTagOptionLists = await _context.TagOptionLists
                    .Where(ol => ol.UserId == userId)
                    .ToListAsync();

                _context.TagOptionLists.RemoveRange(userTagOptionLists);
                recordsCleared += userTagOptionLists.Count;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("User data cleared successfully");
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

    private static List<ActivityBackup> CompressActivitiesToStreaks(List<ActivityBackup> activities)
    {
        if (activities.Count <= 1)
        {
            return activities;
        }

        var result = new List<ActivityBackup>();
        var sorted = activities
            .OrderBy(a => a.TagName, StringComparer.Ordinal)
            .ThenBy(a => a.DateStarted)
            .ToList();

        var streakStart = sorted[0];
        var streakEnd = streakStart.DateStarted.Date;

        for (int i = 1; i < sorted.Count; i++)
        {
            var current = sorted[i];
            var currentDate = current.DateStarted.Date;
            var isConsecutive = currentDate == streakEnd.AddDays(1);
            var isSameGroup = string.Equals(current.TagName, streakStart.TagName, StringComparison.Ordinal)
                && current.Description == streakStart.Description
                && current.DateFinished == streakStart.DateFinished;

            if (isConsecutive && isSameGroup)
            {
                streakEnd = currentDate;
            }
            else
            {
                EmitStreak(result, streakStart, streakEnd);
                streakStart = current;
                streakEnd = currentDate;
            }
        }

        EmitStreak(result, streakStart, streakEnd);

        return result;
    }

    private static void EmitStreak(List<ActivityBackup> result, ActivityBackup streakStart, DateTime streakEnd)
    {
        if (streakEnd > streakStart.DateStarted.Date)
        {
            result.Add(new ActivityBackup
            {
                DateCreated = streakStart.DateCreated,
                DateStarted = streakStart.DateStarted,
                DateFinished = streakStart.DateFinished,
                StreakEndDate = streakEnd,
                Description = streakStart.Description,
                TagName = streakStart.TagName
            });
        }
        else
        {
            result.Add(streakStart);
        }
    }
}
