using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class NotificationService : INotificationService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly IEventLogService _eventLogService;

    public NotificationService(LogMyDayDbContext context, ILogger<NotificationService> logger, IEventLogService eventLogService)
    {
        _context = context;
        _logger = logger;
        _eventLogService = eventLogService;
    }

    public async Task<IList<NotificationResponse>> GetAll(Guid userId)
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

    public async Task<IList<NotificationResponse>> GetByTag(int tagId, Guid userId)
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

    public async Task<NotificationResponse> GetById(int id, Guid userId)
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

    public async Task<NotificationResponse> Create(NotificationRequest request, Guid userId)
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

        var tag = await _context.Tags.FindAsync(entity.TagId);
        await _eventLogService.Log(userId, EventLogLevel.Info, $"Notification for tag '{tag?.TagName ?? entity.TagId.ToString()}' created");

        return MapToResponse(entity);
    }

    public async Task<NotificationResponse> Update(int id, NotificationRequest request, Guid userId)
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

    public async Task<NotificationResponse> RecordDelivery(int id, NotificationDeliveryRequest request, Guid userId)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.DeliveriesOnDate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.DeliveriesOnDate), "DeliveriesOnDate cannot be negative");
        }

        var notification = await _context.Notifications
            .Include(n => n.Tag)
            .FirstOrDefaultAsync(n => n.Id == id && n.Tag.UserId == userId);

        if (notification == null)
        {
            throw new KeyNotFoundException("Notification not found");
        }

        var occurredAtUtc = request.OccurredAtUtc == default
            ? DateTime.UtcNow
            : DateTime.SpecifyKind(request.OccurredAtUtc, DateTimeKind.Utc);

        if (notification.LastDeliveryDate != request.LocalDate)
        {
            notification.LastDeliveryDate = request.LocalDate;
            notification.DeliveriesOnLastDate = request.DeliveriesOnDate;
        }
        else
        {
            notification.DeliveriesOnLastDate = Math.Max(notification.DeliveriesOnLastDate, request.DeliveriesOnDate);
        }

        notification.LastDeliverySentAtUtc = occurredAtUtc;

        var sanitizedInterval = NotificationScheduleCalculator.SanitizeInterval(notification.NudgeInterval);
        var minimumNext = occurredAtUtc.Add(sanitizedInterval);

        var nextEligible = request.NextEligibleSendAfterUtc;
        if (!nextEligible.HasValue || nextEligible.Value < minimumNext)
        {
            nextEligible = minimumNext;
        }

        notification.NextEligibleSendAfterUtc = DateTime.SpecifyKind(nextEligible.Value, DateTimeKind.Utc);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Recorded delivery for notification {NotificationId} on {DeliveryDate} (deliveries={Deliveries})",
            notification.Id,
            request.LocalDate,
            notification.DeliveriesOnLastDate);

        return MapToResponse(notification);
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
            DateCreated = notification.DateCreated,
            LastDeliveryDate = notification.LastDeliveryDate,
            DeliveriesOnLastDate = notification.DeliveriesOnLastDate,
            LastDeliverySentAtUtc = notification.LastDeliverySentAtUtc,
            NextEligibleSendAfterUtc = notification.NextEligibleSendAfterUtc
        };
    }
}
