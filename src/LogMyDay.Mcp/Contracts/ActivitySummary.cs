using LogMyDay.Shared.DTOs;

namespace LogMyDay.Mcp.Contracts;

/// <summary>One logged row, compact: which tag, what value, when.</summary>
public sealed record ActivitySummary(int Id, int? TagId, string Tag, string Value, DateTime DateStarted, DateTime? DateFinished)
{
    public static ActivitySummary From(ActivityResponse activity) => new(
        activity.Id,
        activity.PrimaryTagId,
        activity.PrimaryTagName,
        activity.Description ?? string.Empty,
        activity.DateStarted,
        activity.DateFinished);
}
