using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagDayLockService
{
    Task<IList<TagDayLockResponse>> GetForDate(Guid userId, DateOnly date);

    Task<TagDayLockResponse> Upsert(Guid userId, TagDayLockRequest request, DayLockSetBy setBy);

    Task Delete(Guid userId, int tagId, DateOnly date);

    /// <summary>Returns the current lock state for (userId, tagId, date) or null if no row exists.</summary>
    Task<TagDayLock?> Find(Guid userId, int tagId, DateOnly date);
}
