using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Formatting;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

public class TagTriggerService : ITagTriggerService
{
    private readonly LogMyDayDbContext _context;
    private readonly IUserDirectory _users;
    private readonly IEventLogService _eventLogService;

    public TagTriggerService(LogMyDayDbContext context, IUserDirectory users, IEventLogService eventLogService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _eventLogService = eventLogService ?? throw new ArgumentNullException(nameof(eventLogService));
    }

    public async Task<IList<TagTriggerResponse>> GetAll(Guid userId)
    {
        var triggers = await TriggersWithTags()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .ToListAsync();
        var open = await OpenReminderCounts(userId, triggers.Select(t => t.Id).ToList());

        return triggers.Select(t => MapToResponse(t, open.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TagTriggerResponse> GetById(int id, Guid userId)
    {
        var trigger = await LoadTrigger(id, userId);
        var open = await OpenReminderCounts(userId, [id]);

        return MapToResponse(trigger, open.GetValueOrDefault(id));
    }

    public async Task<TagTriggerResponse> Create(TagTriggerRequest request, Guid userId)
    {
        var (ifTag, promptTag, ifOperand) = await Validate(request, userId);
        var now = DateTime.UtcNow;

        var trigger = new TagTrigger
        {
            UserId = userId,
            Name = request.Name.Trim(),
            DateCreated = now
        };
        Apply(trigger, request, ifOperand, now);

        _context.TagTriggers.Add(trigger);
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Trigger '{trigger.Name}' created: {Describe(trigger, ifTag, promptTag)}");

        return await GetById(trigger.Id, userId);
    }

    public async Task<TagTriggerResponse> Update(int id, TagTriggerRequest request, Guid userId)
    {
        var trigger = await LoadTrigger(id, userId);
        var (ifTag, promptTag, ifOperand) = await Validate(request, userId);

        trigger.Name = request.Name.Trim();
        Apply(trigger, request, ifOperand, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Trigger '{trigger.Name}' updated: {Describe(trigger, ifTag, promptTag)}{(trigger.IsEnabled ? string.Empty : " (paused)")}");

        return await GetById(id, userId);
    }

    public async Task Delete(int id, Guid userId)
    {
        var trigger = await LoadTrigger(id, userId);
        var reminders = await _context.Reminders
            .Include(r => r.Days)
            .Where(r => r.TriggerId == id)
            .ToListAsync();

        // Reminders still to come go with the trigger; done ones stay as history without the link.
        var open = reminders.Where(r => !r.Days.Any(d => d.IsDone)).ToList();
        _context.Reminders.RemoveRange(open);
        foreach (var done in reminders.Except(open))
        {
            done.TriggerId = null;
            done.TriggerDay = null;
        }

        _context.TagTriggers.Remove(trigger);
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Trigger '{trigger.Name}' deleted with {open.Count} open {(open.Count == 1 ? "reminder" : "reminders")}");
    }

    private static void Apply(TagTrigger trigger, TagTriggerRequest request, string? ifOperand, DateTime now)
    {
        trigger.IsEnabled = request.IsEnabled;
        trigger.IfTagId = request.IfTagId;
        trigger.IfOperator = request.IfOperator;
        trigger.IfOperand = ifOperand;
        trigger.PromptTagId = request.PromptTagId;
        trigger.Timing = request.Timing;
        trigger.OffsetMinutes = request.Timing == TagTriggerTiming.AfterEntry ? request.OffsetMinutes : null;
        trigger.AtTime = request.Timing == TagTriggerTiming.AtTime && request.AtTime is TimeOnly at
            ? new TimeOnly(at.Hour, at.Minute)
            : null;
        trigger.DateUpdated = now;
    }

    // Both tags belong to the caller; the rest is checked by TagTriggerValidator, shared with the
    // backup import.
    private async Task<(Tag IfTag, Tag PromptTag, string? IfOperand)> Validate(TagTriggerRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > TagTriggerValidator.MaxNameLength)
        {
            throw new ArgumentException($"A trigger needs a name of at most {TagTriggerValidator.MaxNameLength} characters.");
        }

        var tags = await _context.Tags
            .Where(t => (t.Id == request.IfTagId || t.Id == request.PromptTagId) && t.UserId == userId)
            .ToDictionaryAsync(t => t.Id);
        if (!tags.TryGetValue(request.IfTagId, out var ifTag) || !tags.TryGetValue(request.PromptTagId, out var promptTag))
        {
            throw new KeyNotFoundException("Tag not found");
        }

        var options = ifTag.OptionListId is int listId
            ? await _context.TagOptions.Where(o => o.OptionListId == listId).ToListAsync()
            : null;
        var ifOperand = TagTriggerValidator.Validate(request.IfOperator, request.IfOperand, ifTag, promptTag, options,
            request.Timing, request.OffsetMinutes, request.AtTime);

        return (ifTag, promptTag, ifOperand);
    }

    // Reminders of the triggers that are still to come: today or later and neither done nor skipped.
    private async Task<Dictionary<int, int>> OpenReminderCounts(Guid userId, List<int> triggerIds)
    {
        var today = await UserDay.Today(_users, userId);

        return await _context.Reminders
            .Where(r => r.TriggerId != null && triggerIds.Contains(r.TriggerId.Value) && r.MonitorToDate >= today
                && !r.Days.Any(d => d.IsDone || d.IsSkipped))
            .GroupBy(r => r.TriggerId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);
    }

    private IQueryable<TagTrigger> TriggersWithTags()
    {
        return _context.TagTriggers
            .Include(t => t.IfTag).ThenInclude(t => t!.Group)
            .Include(t => t.PromptTag).ThenInclude(t => t!.Group);
    }

    private async Task<TagTrigger> LoadTrigger(int id, Guid userId)
    {
        return await TriggersWithTags().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId)
            ?? throw new KeyNotFoundException("Trigger not found");
    }

    private static string Describe(TagTrigger trigger, Tag ifTag, Tag promptTag)
    {
        return TagTriggerText.Sentence($"'{ifTag.TagName}'", trigger.IfOperator.ToString(), trigger.IfOperand,
            $"'{promptTag.TagName}'", trigger.Timing.ToString(), trigger.OffsetMinutes, trigger.AtTime);
    }

    /// <summary>"group: name", or the name of a tag outside a group.</summary>
    internal static string TagTitle(Tag? tag)
    {
        if (tag == null)
        {
            return string.Empty;
        }

        return tag.Group?.Name != null ? $"{tag.Group.Name}: {tag.TagName}" : tag.TagName;
    }

    private static TagTriggerResponse MapToResponse(TagTrigger trigger, int openReminders)
    {
        return new TagTriggerResponse
        {
            Id = trigger.Id,
            Name = trigger.Name,
            IsEnabled = trigger.IsEnabled,
            IfTagId = trigger.IfTagId,
            IfTagName = TagTitle(trigger.IfTag),
            IfOperator = trigger.IfOperator.ToString(),
            IfOperand = trigger.IfOperand,
            PromptTagId = trigger.PromptTagId,
            PromptTagName = TagTitle(trigger.PromptTag),
            PromptIsRecordType = trigger.PromptTag?.IsRecordType == true,
            Timing = trigger.Timing.ToString(),
            OffsetMinutes = trigger.OffsetMinutes,
            AtTime = trigger.AtTime,
            OpenReminderCount = openReminders,
            DateCreated = trigger.DateCreated,
            DateUpdated = trigger.DateUpdated
        };
    }
}
