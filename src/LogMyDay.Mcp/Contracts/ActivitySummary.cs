using LogMyDay.Shared.DTOs;

namespace LogMyDay.Mcp.Contracts;

/// <summary>One logged row, compact: which tag, what value, when. <c>Generated</c> marks a value
/// a Tag Activity Relations rule calculated; it is read-only (change its sources or the rule).
/// <c>RecordId</c> names the record a field's value belongs to (get_record, update_record).</summary>
public sealed record ActivitySummary(int Id, int? TagId, string Tag, string Value, DateTime DateStarted, DateTime? DateFinished, bool Generated = false, int? RecordId = null)
{
    public static ActivitySummary From(ActivityResponse activity) => new(
        activity.Id,
        activity.PrimaryTagId,
        activity.PrimaryTagName,
        activity.Description ?? string.Empty,
        activity.DateStarted,
        activity.DateFinished,
        activity.IsGenerated,
        activity.RecordId);
}
