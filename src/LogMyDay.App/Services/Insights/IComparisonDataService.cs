namespace LogMyDay.App.Services.Insights;

public interface IComparisonDataService
{
    /// <summary>
    /// Fetches every tag's activities across one date range and buckets them per day. The range must
    /// already account for row offsets — see <see cref="ComparisonTimelineCalculator.GetRequiredRange"/>.
    /// </summary>
    Task<ComparisonDataSet> LoadDailyValues(
        IReadOnlyList<int> tagIds,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken = default);
}
