using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagCheckService
{
    Task<IList<TagCheckResponse>> GetAll(Guid userId);
    Task<TagCheckResponse> GetById(int id, Guid userId);
    Task<TagCheckResponse> Create(TagCheckRequest request, Guid userId);
    Task<TagCheckResponse> Update(int id, TagCheckRequest request, Guid userId);
    Task Delete(int id, Guid userId);

    /// <summary>The days in the range that break an active check, newest first. Defaults: the
    /// last 30 days up to today. The range ends today at the latest and spans at most a year.</summary>
    Task<TagCheckReviewResponse> Review(DateOnly? from, DateOnly? to, Guid userId);

    /// <summary>The enabled Warn checks a saved activity left broken on its day: those that read
    /// its tag, or a tag a rule computes from it. Flag checks never warn, and Requires never warns
    /// about today. Null when nothing is broken, so responses leave the field out. Call it after
    /// the save is committed; it never fails the save.</summary>
    Task<List<TagCheckWarning>?> WarningsAfterSave(Guid userId, ActivityResponse saved);

    /// <summary>As for an activity, for checks that read any field of the saved record's type
    /// (a field the save emptied included).</summary>
    Task<List<TagCheckWarning>?> WarningsAfterSave(Guid userId, RecordResponse saved);
}
