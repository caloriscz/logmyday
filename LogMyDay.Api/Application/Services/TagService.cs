using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class TagService : ITagService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<TagService> _logger;

    public TagService(LogMyDayDbContext context, ILogger<TagService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> Create(TagRequest createTagRequest, Guid userId)
    {
        _logger.LogInformation("Creating tag with request: {@CreateTagRequest}", createTagRequest);

        var tag = new Tag
        {
            TagName = createTagRequest.Tag,
            InputTypeId = createTagRequest.TypeId == 0 ? null : createTagRequest.TypeId,
            IsRequired = createTagRequest.IsRequired, // Map IsRequired
            IsRepeatable = createTagRequest.IsRepeatable,
            TimeGranularity = createTagRequest.TimeGranularity,
            IsRange = createTagRequest.IsRange,
            UnitId = createTagRequest.UnitId,
            MinValue = createTagRequest.MinValue,
            MaxValue = createTagRequest.MaxValue,
            Step = createTagRequest.Step,
            DefaultValue = createTagRequest.DefaultValue,
            OptionListId = createTagRequest.OptionListId,
            UserId = userId // Associate tag with current user
        };

        _logger.LogInformation("Created tag entity: {@Tag}", tag);

        await _context.Tags.AddAsync(tag);
        await _context.SaveChangesAsync();

        return tag.Id;
    }

    /// <summary>
    /// Returns all tags for a specific user
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IList<TagResponse>> GetAll(Guid userId)
    {
        var tags = await _context.Tags
            .Include(t => t.Unit)
            .Include(t => t.OptionList)
            .Where(t => t.UserId == userId)
            .OrderBy(x => x.TagName)
            .ToListAsync();

        var tagsResponse = tags.Select(tag => new TagResponse
            {
                Id = tag.Id,
                Title = tag.TagName,
                InputTypeId = tag.InputTypeId,
                TypeId = tag.InputTypeId,
                IsRequired = tag.IsRequired, // Map IsRequired
                IsRepeatable = tag.IsRepeatable,
                TimeGranularity = tag.TimeGranularity,
                IsRange = tag.IsRange,
                UnitId = tag.UnitId,
                UnitSymbol = tag.Unit?.Symbol,
                MinValue = tag.MinValue,
                MaxValue = tag.MaxValue,
                Step = tag.Step,
                DefaultValue = tag.DefaultValue,
                OptionListId = tag.OptionListId,
                OptionListName = tag.OptionList?.Name
            }).ToList();

        return tagsResponse;
    }

    /// <summary>
    /// Updates a tag name if it was found
    /// </summary>
    /// <param name="id"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    /// <exception cref="AppException"></exception>
    public async Task Update(int id, TagRequest model, Guid userId)
    {
        var tag = await _context.Tags
            .Where(t => t.Id == id && t.UserId == userId)
            .FirstOrDefaultAsync();

        if (tag == null)
        {
            throw new KeyNotFoundException("Tag not found");
        }

        tag.TagName = model.Tag;
        tag.InputTypeId = model.TypeId == 0 ? null : model.TypeId;
        tag.IsRequired = model.IsRequired; // Map IsRequired
        tag.IsRepeatable = model.IsRepeatable;
        tag.TimeGranularity = model.TimeGranularity;
        tag.IsRange = model.IsRange;
        tag.UnitId = model.UnitId;
        tag.MinValue = model.MinValue;
        tag.MaxValue = model.MaxValue;
        tag.Step = model.Step;
        tag.DefaultValue = model.DefaultValue;
        tag.OptionListId = model.OptionListId;

        _context.Tags.Update(tag);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete tag by its id
    /// </summary>
    /// <param name="id"></param>
    public async Task Delete(int id, Guid userId)
    {
        Tag? link = await _context.Tags
            .Where(t => t.Id == id && t.UserId == userId)
            .SingleOrDefaultAsync();

        if (link != null)
        {
            _context.Tags.Remove(link);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Get tag by its id
    /// </summary>
    /// <param name="tagId"></param>
    /// <returns></returns>
    public async Task<TagResponse> GetTagById(int tagId, Guid userId)
    {
        Tag? tagResponse = await _context.Tags
            .Include(t => t.Unit)
            .Include(t => t.OptionList)
            .Where(t => t.Id == tagId && t.UserId == userId)
            .FirstOrDefaultAsync();

        if (tagResponse == null)
        {
            throw new KeyNotFoundException("Tag not found");
        }

        TagResponse response = new()
        {
            Id = tagResponse.Id,
            Title = tagResponse.TagName,
            InputTypeId = tagResponse.InputTypeId,
            TypeId = tagResponse.InputTypeId,
            IsRequired = tagResponse.IsRequired, // Map IsRequired
            IsRepeatable = tagResponse.IsRepeatable,
            TimeGranularity = tagResponse.TimeGranularity,
            IsRange = tagResponse.IsRange,
            UnitId = tagResponse.UnitId,
            UnitSymbol = tagResponse.Unit?.Symbol,
            MinValue = tagResponse.MinValue,
            MaxValue = tagResponse.MaxValue,
            Step = tagResponse.Step,
            DefaultValue = tagResponse.DefaultValue,
            OptionListId = tagResponse.OptionListId,
            OptionListName = tagResponse.OptionList?.Name
        };

        return response;
    }

    /// <summary>
    /// Get paginated, sorted, and filtered tags
    /// </summary>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <param name="orderBy"></param>
    /// <param name="filter"></param>
    /// <param name="filterType"></param>
    /// <returns></returns>
    public async Task<PagedResult<TagResponse>> GetPaged(int pageNumber, int pageSize, string orderBy, Guid userId, string? filter = null, string? filterType = null)
    {
        var query = _context.Tags
            .Include(t => t.Unit)
            .Include(t => t.OptionList)
            .Where(t => t.UserId == userId)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filterType == "exact")
                query = query.Where(t => t.TagName == filter);
            else
                query = query.Where(t => t.TagName.Contains(filter));
        }
        if (orderBy?.ToLower() == "asc")
            query = query.OrderBy(t => t.TagName);
        else
            query = query.OrderByDescending(t => t.TagName);
        var totalCount = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<TagResponse>
        {
            Items = items.Select(t => new TagResponse
            {
                Id = t.Id,
                Title = t.TagName,
                TypeId = t.InputTypeId,
                InputTypeId = t.InputTypeId,
                IsRequired = t.IsRequired, // Map IsRequired
                IsRepeatable = t.IsRepeatable,
                TimeGranularity = t.TimeGranularity,
                IsRange = t.IsRange,
                UnitId = t.UnitId,
                UnitSymbol = t.Unit?.Symbol,
                MinValue = t.MinValue,
                MaxValue = t.MaxValue,
                Step = t.Step,
                DefaultValue = t.DefaultValue,
                OptionListId = t.OptionListId,
                OptionListName = t.OptionList?.Name
            }).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
