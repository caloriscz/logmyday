using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IActivitySummaryService
{
    /// <summary>
    /// Statistics for one of the user's tags over an inclusive date range. Throws
    /// <see cref="KeyNotFoundException"/> for a tag the user does not own and
    /// <see cref="ArgumentException"/> for a range the bucket size does not allow.
    /// </summary>
    Task<ActivitySummaryResponse> Summarize(ActivitySummaryRequest request, Guid userId);
}
