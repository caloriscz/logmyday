namespace LogMyDay.Api.Application.Services;

/// <summary>Thrown by <see cref="ActivityService"/> when a write targets a computed tag (one a
/// Tag Activity Relations rule owns) or a row a rule generated. Results are read-only: the user
/// changes the rule, not the result. Controllers translate to HTTP 409 with the code
/// <c>tag-computed</c>.</summary>
public class TagComputedException : Exception
{
    public int TagId { get; }

    public TagComputedException(int tagId)
        : base($"Tag {tagId} is computed by a rule; its values are generated and cannot be edited. Change the rule instead.")
    {
        TagId = tagId;
    }
}
