using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagService
{
    Task<int> Create(TagRequest request, Guid userId);
    Task Update(int id, TagRequest model, Guid userId);
    Task<IList<TagResponse>> GetAll(Guid userId);
    Task<PagedResult<TagResponse>> GetPaged(int pageNumber, int pageSize, string orderBy, Guid userId, string? filter = null, string? filterType = null, int? groupId = null);
    Task<TagResponse> GetTagById(int tagId, Guid userId);
    Task Delete(int id, Guid userId);
}
