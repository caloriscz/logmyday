using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface ITagGroupApi
{
    [Get("/api/tag-groups")]
    Task<IList<TagGroupResponse>> GetTagGroups();

    [Get("/api/tag-groups/{id}")]
    Task<TagGroupResponse> GetTagGroupById(int id);

    [Post("/api/tag-groups")]
    Task<int> CreateTagGroup([Body] TagGroupRequest request);

    [Put("/api/tag-groups/{id}")]
    Task UpdateTagGroup(int id, [Body] TagGroupRequest request);

    [Delete("/api/tag-groups/{id}")]
    Task DeleteTagGroup(int id);
}
