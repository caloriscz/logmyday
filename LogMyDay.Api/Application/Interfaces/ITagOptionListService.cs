using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagOptionListService
{
    Task<IEnumerable<TagOptionListResponse>> GetAllAsync(Guid userId);

    Task<TagOptionListResponse> GetByIdAsync(int id, Guid userId);

    Task<int> CreateAsync(TagOptionListRequest request, Guid userId);

    Task UpdateAsync(int id, TagOptionListRequest request, Guid userId);

    Task DeleteAsync(int id, Guid userId);
}
