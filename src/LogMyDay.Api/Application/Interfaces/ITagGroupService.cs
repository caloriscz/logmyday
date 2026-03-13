using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagGroupService
{
    Task<IList<TagGroupResponse>> GetAll(Guid userId);
    Task<TagGroupResponse> GetById(int id, Guid userId);
    Task<int> Create(TagGroupRequest request, Guid userId);
    Task Update(int id, TagGroupRequest request, Guid userId);
    Task Delete(int id, Guid userId);
}
