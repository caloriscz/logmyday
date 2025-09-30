using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface INotificationService
{
    Task<IList<NotificationResponse>> GetAllAsync(Guid userId);
    Task<IList<NotificationResponse>> GetByTagAsync(int tagId, Guid userId);
    Task<NotificationResponse> GetByIdAsync(int id, Guid userId);
    Task<NotificationResponse> CreateAsync(NotificationRequest request, Guid userId);
    Task<NotificationResponse> UpdateAsync(int id, NotificationRequest request, Guid userId);
    Task DeleteAsync(int id, Guid userId);
}
