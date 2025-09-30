using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class NotificationService : INotificationService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(LogMyDayDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IList<NotificationResponse>> GetAllAsync(Guid userId)
    {
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Include(n => n.Tag)
            .Where(n => n.Tag != null && n.Tag.UserId == userId)
            .OrderBy(n => n.Tag!.TagName)
            .ThenBy(n => n.Id)
            .ToListAsync();

        return notifications.Select(MapToResponse).ToList();
    }

    public async Task<IList<NotificationResponse>> GetByTagAsync(int tagId, Guid userId)
    {
        await EnsureTagAccessible(tagId, userId);

        var notifications = await _context.Notifications
            .AsNoTracking()
            .Include(n => n.Tag)
            .Where(n => n.TagId == tagId)
            .OrderBy(n => n.Id)
            .ToListAsync();

        return notifications.Select(MapToResponse).ToList();
    }

    public async Task<NotificationResponse> GetByIdAsync(int id, Guid userId)
    {
        var notification = await _context.Notifications
            .AsNoTracking()
            .Include(n => n.Tag)
            .FirstOrDefaultAsync(n => n.Id == id && n.Tag.UserId == userId);

        if (notification == null)
        {
            throw new KeyNotFoundException("Notification not found");
        }

        return MapToResponse(notification);
    }

    public async Task<NotificationResponse> CreateAsync(NotificationRequest request, Guid userId)
    {
        await EnsureTagAccessible(request.TagId, userId);
        ValidateRequest(request);

        var entity = new Notification
        {
            TagId = request.TagId,
            NotificationText = request.NotificationText,
            NotBeforeTime = request.NotBeforeTime,
            NotAfterTime = request.NotAfterTime,
            MaxNudges = request.MaxNudges,
            NudgeInterval = request.NudgeInterval,
            IsActive = request.IsActive,
            DateCreated = DateTime.UtcNow
        };

        _context.Notifications.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created notification {NotificationId} for tag {TagId}", entity.Id, entity.TagId);

        return MapToResponse(entity);
    }

    public async Task<NotificationResponse> UpdateAsync(int id, NotificationRequest request, Guid userId)
    {
        ValidateRequest(request);

        var notification = await _context.Notifications
            .Include(n => n.Tag)
            .FirstOrDefaultAsync(n => n.Id == id && n.Tag.UserId == userId);

        if (notification == null)
        {
            throw new KeyNotFoundException("Notification not found");
        }

        // Ensure the notification remains associated with a tag belonging to the same user
        if (notification.TagId != request.TagId)
        {
            await EnsureTagAccessible(request.TagId, userId);
            notification.TagId = request.TagId;
        }

        notification.NotificationText = request.NotificationText;
        notification.NotBeforeTime = request.NotBeforeTime;
        notification.NotAfterTime = request.NotAfterTime;
        notification.MaxNudges = request.MaxNudges;
        notification.NudgeInterval = request.NudgeInterval;
        notification.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated notification {NotificationId}", notification.Id);

        return MapToResponse(notification);
    }

    public async Task DeleteAsync(int id, Guid userId)
    {
        var notification = await _context.Notifications
            .Include(n => n.Tag)
            .FirstOrDefaultAsync(n => n.Id == id && n.Tag.UserId == userId);

        if (notification == null)
        {
            return;
        }

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted notification {NotificationId}", id);
    }

    private async Task<Tag> EnsureTagAccessible(int tagId, Guid userId)
    {
        var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);
        if (tag == null)
        {
            throw new KeyNotFoundException("Tag not found");
        }

        return tag;
    }

    private static void ValidateRequest(NotificationRequest request)
    {
        if (request.MaxNudges < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaxNudges), "Max nudges cannot be negative");
        }

        if (request.NudgeInterval.HasValue)
        {
            if (request.NudgeInterval.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(request.NudgeInterval), "Nudge interval must be positive");
            }

            if (request.NudgeInterval.Value < TimeSpan.FromMinutes(NotificationScheduleCalculator.MinimumIntervalMinutes))
            {
                throw new ArgumentOutOfRangeException(nameof(request.NudgeInterval), $"Nudge interval must be at least {NotificationScheduleCalculator.MinimumIntervalMinutes} minutes");
            }
        }

        if (request.NotBeforeTime.HasValue && request.NotAfterTime.HasValue && request.NotAfterTime < request.NotBeforeTime)
        {
            throw new ArgumentException("NotAfterTime must be later than or equal to NotBeforeTime");
        }
    }

    private static NotificationResponse MapToResponse(Notification notification)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            TagId = notification.TagId,
            TagName = notification.Tag?.TagName,
            NotificationText = notification.NotificationText,
            NotBeforeTime = notification.NotBeforeTime,
            NotAfterTime = notification.NotAfterTime,
            MaxNudges = notification.MaxNudges,
            NudgeInterval = notification.NudgeInterval,
            IsActive = notification.IsActive,
            DateCreated = notification.DateCreated
        };
    }
}
