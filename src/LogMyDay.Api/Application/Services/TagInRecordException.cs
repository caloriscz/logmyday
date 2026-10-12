namespace LogMyDay.Api.Application.Services;

/// <summary>Thrown by <see cref="ActivityService"/> when a write targets a field of a record type,
/// or a value that belongs to a record. A record's values are written together, through the
/// record. Controllers translate to HTTP 409 with the code <c>tag-in-record</c>.</summary>
public class TagInRecordException : Exception
{
    public int TagId { get; }

    public TagInRecordException(int tagId)
        : base($"Tag {tagId} is a field of a record type; its values are logged together as a record, not on their own.")
    {
        TagId = tagId;
    }
}
