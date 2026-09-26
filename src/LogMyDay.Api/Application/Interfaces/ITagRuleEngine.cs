using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Application.Interfaces;

/// <summary>Evaluates Tag Activity Relations rules and writes their generated rows. It writes
/// through the DbContext directly, never through <c>ActivityService.Create</c>: a result is
/// replaced, never accumulated, and day locks do not apply to computed tags.</summary>
public interface ITagRuleEngine
{
    /// <summary>Re-evaluates every enabled rule that has one of the changed tags as a source, for
    /// each changed local day on or after the rule's EffectiveFrom. Call it after the source write
    /// is saved; it saves its own changes.</summary>
    Task OnSourcesChanged(Guid userId, IReadOnlyCollection<(int TagId, DateOnly Day)> changes);

    /// <summary>What recomputing the range would change, without writing anything.</summary>
    Task<TagRuleRangeResult> Preview(TagRule rule, DateOnly from, DateOnly to);

    /// <summary>Recomputes the range one calendar month at a time. Each month is saved in its own
    /// transaction (on relational providers), so a long range never holds the database for long.</summary>
    Task<TagRuleRangeResult> Recompute(TagRule rule, DateOnly from, DateOnly to);
}

/// <summary>Counts of result rows a range evaluation creates, changes, removes or leaves as they
/// are, and how many source values it skipped because they are not numbers.</summary>
public record TagRuleRangeResult(int Created, int Updated, int Deleted, int Unchanged, int SkippedSourceValues)
{
    public static TagRuleRangeResult Empty { get; } = new(0, 0, 0, 0, 0);

    public TagRuleRangeResult Add(TagRuleRangeResult other) => new(
        Created + other.Created,
        Updated + other.Updated,
        Deleted + other.Deleted,
        Unchanged + other.Unchanged,
        SkippedSourceValues + other.SkippedSourceValues);
}
