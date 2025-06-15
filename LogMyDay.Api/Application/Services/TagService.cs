using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Refit;

namespace LogMyDay.Api.Application.Services;

public class TagService : ITagService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<TagService> _logger;

    public TagService(LogMyDayDbContext context, ILogger<TagService> logger)
    {
        _context = context;
        _logger = logger;
    }    public async Task<int> Create(TagRequest createTagRequest)
    {
        _logger.LogInformation("Creating tag with request: {@CreateTagRequest}", createTagRequest);
        
        var tag = new Tag
        {
            TagName = createTagRequest.Tag,
            InputTypeId = createTagRequest.TypeId == 0 ? null : createTagRequest.TypeId,
            IsRequired = createTagRequest.IsRequired, // Map IsRequired
            IsRepeatable = createTagRequest.IsRepeatable,
            TimeGranularity = createTagRequest.TimeGranularity,
            IsRange = createTagRequest.IsRange
        };

        _logger.LogInformation("Created tag entity: {@Tag}", tag);

        await _context.Tags.AddAsync(tag);
        await _context.SaveChangesAsync();

        return tag.Id;
    }

    public async Task<IList<TagResponse>> GetAll()
    {
        var tags = await _context.Tags.OrderBy(x => x.TagName).ToListAsync();        var tagsResponse = tags.Select(tag => new TagResponse
        {
            Id = tag.Id,
            Title = tag.TagName,
            InputTypeId = tag?.InputType?.Id,
            TypeId = tag.InputTypeId,
            IsRequired = tag.IsRequired, // Map IsRequired
            IsRepeatable = tag.IsRepeatable,
            TimeGranularity = tag.TimeGranularity,
            IsRange = tag.IsRange
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
    public async Task Update(int id, TagRequest model)
    {
        var tag = await _context.Tags.FindAsync(id);        tag.TagName = model.Tag;
        tag.InputTypeId = model.TypeId;
        tag.IsRequired = model.IsRequired; // Map IsRequired
        tag.IsRepeatable = model.IsRepeatable;
        tag.TimeGranularity = model.TimeGranularity;
        tag.IsRange = model.IsRange;

        _context.Tags.Update(tag);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete tag by its id
    /// </summary>
    /// <param name="id"></param>
    public async Task Delete(int id)
    {
        Tag? link = await _context.Tags.SingleOrDefaultAsync(x => x.Id == id);

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
    public async Task<TagResponse> GetTagById(int tagId)
    {
        Tag? tagResponse = await _context.Tags.FindAsync(tagId);        TagResponse response = new()
        {
            Id = tagResponse.Id,
            Title = tagResponse.TagName,
            InputTypeId = tagResponse?.InputType?.Id,
            TypeId = tagResponse.InputTypeId,
            IsRequired = tagResponse.IsRequired, // Map IsRequired
            IsRepeatable = tagResponse.IsRepeatable,
            TimeGranularity = tagResponse.TimeGranularity,
            IsRange = tagResponse.IsRange
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
    public async Task<PagedResult<TagResponse>> GetPaged(int pageNumber, int pageSize, string orderBy, string? filter = null, string? filterType = null)
    {
        var query = _context.Tags.AsQueryable();
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
        {            Items = items.Select(t => new TagResponse {
                Id = t.Id,
                Title = t.TagName,
                TypeId = t.InputTypeId,
                InputTypeId = t.InputTypeId,
                IsRequired = t.IsRequired, // Map IsRequired
                IsRepeatable = t.IsRepeatable,
                TimeGranularity = t.TimeGranularity,
                IsRange = t.IsRange
            }).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
