namespace LogMyDay.App.Services.Insights;

public interface IComparisonPreferencesService
{
    /// <summary>
    /// Restores the stored comparison setup, validating it against the tags that still exist. Never
    /// throws — an unreadable or stale blob yields <see cref="ComparisonPreferences.Default"/>.
    /// </summary>
    Task<ComparisonPreferences> Load(IEnumerable<int> validTagIds);

    Task Save(ComparisonPreferences preferences);
}
