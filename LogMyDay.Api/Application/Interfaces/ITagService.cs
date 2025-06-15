using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagService
{
    Task<int> Create(TagRequest request);
    Task Update(int id, TagRequest model);
    Task<IList<TagResponse>> GetAll();
    Task<PagedResult<TagResponse>> GetPaged(int pageNumber, int pageSize, string orderBy, string? filter = null, string? filterType = null);
    Task<TagResponse> GetTagById(int tagId);
    Task Delete(int id);
}
