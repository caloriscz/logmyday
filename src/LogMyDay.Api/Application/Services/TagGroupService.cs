using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Api.Infrastructure.Specifications;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Services;

public class TagGroupService : ITagGroupService
{
    private readonly IRepository<TagGroup> _repository;

    public TagGroupService(IRepository<TagGroup> repository)
    {
        _repository = repository;
    }

    public async Task<IList<TagGroupResponse>> GetAll(Guid userId)
    {
        var spec = new TagGroupSpecifications.TagGroupsForUserSpec(userId);
        var groups = await _repository.GetAsync(spec);

        return groups
            .OrderBy(g => g.DisplayOrder ?? int.MaxValue)
            .ThenBy(g => g.Name)
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<TagGroupResponse> GetById(int id, Guid userId)
    {
        var spec = new TagGroupSpecifications.TagGroupByIdAndUserSpec(id, userId);
        var group = await _repository.GetSingleAsync(spec);

        if (group == null)
        {
            throw new KeyNotFoundException("Tag group not found");
        }

        return MapToResponse(group);
    }

    public async Task<int> Create(TagGroupRequest request, Guid userId)
    {
        var group = new TagGroup
        {
            Name = request.Name,
            UserId = userId,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            DateCreated = DateTime.UtcNow
        };

        await _repository.AddAsync(group);

        return group.Id;
    }

    public async Task Update(int id, TagGroupRequest request, Guid userId)
    {
        var spec = new TagGroupSpecifications.TagGroupByIdAndUserSpec(id, userId);
        var group = await _repository.GetSingleAsync(spec);

        if (group == null)
        {
            throw new KeyNotFoundException("Tag group not found");
        }

        group.Name = request.Name;
        group.Description = request.Description;
        group.DisplayOrder = request.DisplayOrder;

        await _repository.UpdateAsync(group);
    }

    public async Task Delete(int id, Guid userId)
    {
        var spec = new TagGroupSpecifications.TagGroupByIdAndUserSpec(id, userId);
        var group = await _repository.GetSingleAsync(spec);

        if (group == null)
        {
            throw new KeyNotFoundException("Tag group not found");
        }

        await _repository.DeleteAsync(group);
    }

    private static TagGroupResponse MapToResponse(TagGroup group) =>
        new()
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            DisplayOrder = group.DisplayOrder,
            DateCreated = group.DateCreated
        };
}
