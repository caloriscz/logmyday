using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface INotificationService
{
    Task<IList<NotificationResponse>> GetAll(Guid userId);
    Task<IList<NotificationResponse>> GetByTag(int tagId, Guid userId);
    Task<NotificationResponse> GetById(int id, Guid userId);
    Task<NotificationResponse> Create(NotificationRequest request, Guid userId);
    Task<NotificationResponse> Update(int id, NotificationRequest request, Guid userId);
    Task DeleteAsync(int id, Guid userId);
    Task<NotificationResponse> RecordDelivery(int id, NotificationDeliveryRequest request, Guid userId);
}
