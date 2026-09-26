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

    /// <summary>Re-evaluates a rule for every day in the range that has a source row or an
    /// existing result, and saves. Returns the number of result rows created, updated and
    /// deleted.</summary>
    Task<(int Created, int Updated, int Deleted)> EvaluateRange(TagRule rule, DateOnly from, DateOnly to);
}
