using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagOptionListService
{
    Task<IEnumerable<TagOptionListResponse>> GetAll(Guid userId);

    Task<TagOptionListResponse> GetById(int id, Guid userId);

    Task<int> Create(TagOptionListRequest request, Guid userId);

    Task Update(int id, TagOptionListRequest request, Guid userId);

    Task Delete(int id, Guid userId);
}
