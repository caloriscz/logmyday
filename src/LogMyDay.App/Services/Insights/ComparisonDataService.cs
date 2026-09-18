using LogMyDay.Domain.Constants;
using LogMyDay.Shared.Interfaces;

namespace LogMyDay.App.Services.Insights;

/// <summary>
/// Loads the raw activity values the comparison view needs. There is no server-side daily aggregate
/// endpoint, so this fetches rows per tag and buckets them by day in the same way Calendar and Charts do.
/// Purely I/O — the range it is given is computed by <see cref="ComparisonTimelineCalculator"/>.
/// </summary>
public class ComparisonDataService : IComparisonDataService
{
    private readonly IActivityApi _activityApi;

    public ComparisonDataService(IActivityApi activityApi)
    {
        _activityApi = activityApi;
    }

    public async Task<ComparisonDataSet> LoadDailyValues(
        IReadOnlyList<int> tagIds,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken = default)
    {
        // The same tag legitimately appears in two rows (this year versus last year), so fetch each once.
        var distinctTagIds = tagIds.Distinct().ToList();

        if (distinctTagIds.Count == 0)
        {
            return ComparisonDataSet.Empty;
        }

        var start = rangeStart.Date;

        // ActivityService filters DateStarted <= endDate against a full DateTime, so a midnight end would
        // drop everything logged during the final day.
        var end = rangeEnd.Date.AddDays(1).AddTicks(-1);

        var valuesByTag = new Dictionary<int, IReadOnlyDictionary<DateTime, IReadOnlyList<string>>>(distinctTagIds.Count);
        var isTruncated = false;

        foreach (var tagId in distinctTagIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _activityApi.GetActivities(
                pageNumber: 1,
                pageSize: ChartConstants.DefaultPageSize,
                orderBy: "asc",
                tagId: tagId,
                startDate: start,
                endDate: end);

            var items = result.Items;

            if (items.Count >= ChartConstants.DefaultPageSize)
            {
                isTruncated = true;
            }

            var byDate = new Dictionary<DateTime, IReadOnlyList<string>>();

            // No timezone conversion, matching Calendar and Journal — diverging here would put the same
            // activity in different day buckets depending on which Insights view you opened.
            foreach (var activity in items)
            {
                var day = activity.DateStarted.Date;

                if (!byDate.TryGetValue(day, out var existing))
                {
                    existing = new List<string>();
                    byDate[day] = existing;
                }

                // orderBy: "asc" means this list stays chronological, which is what First aggregation reads.
                ((List<string>)existing).Add(activity.Description ?? string.Empty);
            }

            valuesByTag[tagId] = byDate;
        }

        return new ComparisonDataSet(valuesByTag, isTruncated);
    }
}
