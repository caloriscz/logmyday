namespace LogMyDay.Api.Application.Services;

/// <summary>Thrown by <see cref="ActivityService"/> when a write targets a record type, a field of
/// one, or a value that belongs to a record. A record's values are written together, through the
/// record. Controllers translate to HTTP 409 with the code <c>tag-in-record</c>.</summary>
public class TagInRecordException : Exception
{
    public int TagId { get; }

    public TagInRecordException(int tagId)
        : base($"Tag {tagId} is a record type or a field of one; values are logged together as a record, not on their own.")
    {
        TagId = tagId;
    }
}
