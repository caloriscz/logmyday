namespace LogMyDay.Api.Application.Interfaces;

/// <summary>Brings the reminders of the user's triggers in line with a save: creates the day's
/// reminder when a trigger's condition starts to hold, withdraws an open one when it stops, and
/// closes one when its prompted tag gets logged. Called after <see cref="ITagRuleEngine.OnSourcesChanged"/>
/// on every forward write path, inside the save's transaction; never on backup import or
/// recompute, where past nudges mean nothing.</summary>
public interface ITagTriggerEngine
{
    /// <param name="changes">The tags and local days the save changed, as given to the rule engine.</param>
    Task OnTagsChanged(Guid userId, IReadOnlyCollection<(int TagId, DateOnly Day)> changes);
}
