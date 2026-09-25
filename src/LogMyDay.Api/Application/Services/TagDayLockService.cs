using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

public class TagDayLockService : ITagDayLockService
{
    private readonly LogMyDayDbContext _context;

    public TagDayLockService(LogMyDayDbContext context)
    {
        _context = context;
    }

    public async Task<IList<TagDayLockResponse>> GetForDate(Guid userId, DateOnly date)
    {
        var rows = await _context.TagDayLocks
            .AsNoTracking()
            .Include(l => l.Tag)
            .Where(l => l.UserId == userId && l.Date == date)
            .ToListAsync();

        return rows.Select(MapToResponse).ToList();
    }

    public async Task<TagDayLockResponse> Upsert(Guid userId, TagDayLockRequest request, DayLockSetBy setBy)
    {
        var row = await _context.TagDayLocks
            .Include(l => l.Tag)
            .FirstOrDefaultAsync(l => l.UserId == userId && l.TagId == request.TagId && l.Date == request.Date);

        if (row == null)
        {
            // Tags are strictly per user: never create a lock row on another user's tag.
            if (!await _context.Tags.AnyAsync(t => t.Id == request.TagId && t.UserId == userId))
            {
                throw new KeyNotFoundException("Tag not found");
            }

            row = new TagDayLock
            {
                UserId = userId,
                TagId = request.TagId,
                Date = request.Date,
                IsLocked = request.IsLocked,
                SetAt = DateTime.UtcNow,
                SetBy = setBy,
                Reason = request.Reason
            };
            _context.TagDayLocks.Add(row);
        }
        else
        {
            row.IsLocked = request.IsLocked;
            row.SetAt = DateTime.UtcNow;
            row.SetBy = setBy;
            row.Reason = request.Reason;
        }

        await _context.SaveChangesAsync();

        if (row.Tag == null)
        {
            row.Tag = await _context.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Id == row.TagId);
        }

        return MapToResponse(row);
    }

    public async Task Delete(Guid userId, int tagId, DateOnly date)
    {
        var row = await _context.TagDayLocks
            .FirstOrDefaultAsync(l => l.UserId == userId && l.TagId == tagId && l.Date == date);

        if (row != null)
        {
            _context.TagDayLocks.Remove(row);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<TagDayLock?> Find(Guid userId, int tagId, DateOnly date)
    {
        return await _context.TagDayLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.TagId == tagId && l.Date == date);
    }

    private static TagDayLockResponse MapToResponse(TagDayLock row) =>
        new()
        {
            Id = row.Id,
            TagId = row.TagId,
            TagName = row.Tag?.TagName,
            Date = row.Date,
            IsLocked = row.IsLocked,
            SetAt = row.SetAt,
            SetBy = row.SetBy,
            Reason = row.Reason
        };
}
