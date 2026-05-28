namespace LogMyDay.Api.Application.Services;

/// <summary>Thrown by <see cref="ActivityService"/> when an activity is created/updated for a
/// (UserId, TagId, Date) triple that has an active <see cref="Domain.Entities.TagDayLock"/>
/// (IsLocked == true). Controllers translate to HTTP 409 with the code
/// <c>tag-day-locked</c> so the client can offer an unlock-and-retry prompt.</summary>
public class TagDayLockedException : Exception
{
    public int TagId { get; }
    public DateOnly Date { get; }

    public TagDayLockedException(int tagId, DateOnly date)
        : base($"Tag {tagId} is locked for {date:yyyy-MM-dd}.")
    {
        TagId = tagId;
        Date = date;
    }
}
