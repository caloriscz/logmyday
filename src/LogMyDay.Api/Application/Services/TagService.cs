using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Helpers;
using LogMyDay.Shared.DTOs;
using Microsoft.Extensions.Logging;
using static LogMyDay.Api.Infrastructure.Specifications.TagSpecifications;

namespace LogMyDay.Api.Application.Services;

public class TagService : ITagService
{
    private readonly LogMyDayDbContext _context;
    private readonly ITagRepository _tagRepository;
    private readonly ILogger<TagService> _logger;

    public TagService(LogMyDayDbContext context, ITagRepository tagRepository, ILogger<TagService> logger)
    {
        _context = context;
        _tagRepository = tagRepository;
        _logger = logger;
    }

    public async Task<int> Create(TagRequest createTagRequest, Guid userId)
    {
        _logger.LogInformation("Creating tag with request: {@CreateTagRequest}", createTagRequest);

        // Apply InputType constraints for locked types
        var inputTypeId = createTagRequest.TypeId == 0 ? (int?)null : createTagRequest.TypeId;
        double? minValue = createTagRequest.MinValue;
        double? maxValue = createTagRequest.MaxValue;
        double? step = createTagRequest.Step;
        bool isRepeatable = createTagRequest.IsRepeatable;

        if (inputTypeId.HasValue)
        {
            var inputType = await _context.InputTypes.FindAsync(inputTypeId.Value);
            if (inputType != null)
            {
                // Enforce min/max/step constraints if range not editable
                if (!inputType.IsRangeEditable || !inputType.IsMinimumEditable || !inputType.IsMaximumEditable || !inputType.IsStepEditable)
                {
                    var constraints = InputTypeDefaults.GetConstraintsForType(inputTypeId.Value);
                    if (!inputType.IsMinimumEditable) minValue = constraints.MinValue;
                    if (!inputType.IsMaximumEditable) maxValue = constraints.MaxValue;
                    if (!inputType.IsStepEditable) step = constraints.Step;
                    _logger.LogInformation("Applied locked constraints for InputType {InputTypeId}: Min={Min}, Max={Max}, Step={Step}",
                        inputTypeId, minValue, maxValue, step);
                }

                // Enforce IsRepeatable constraint if not editable
                if (!inputType.IsRepeatableEditable)
                {
                    isRepeatable = false;
                    _logger.LogInformation("Applied locked IsRepeatable=false for InputType {InputTypeId}", inputTypeId);
                }
            }
        }

        var tag = new Tag
        {
            TagName = createTagRequest.Tag,
            InputTypeId = inputTypeId,
            IsRequired = createTagRequest.IsRequired, // Map IsRequired
            IsRepeatable = isRepeatable,
            TimeGranularity = createTagRequest.TimeGranularity,
            IsRange = createTagRequest.IsRange,
            UnitId = createTagRequest.UnitId,
            MinValue = minValue,
            MaxValue = maxValue,
            Step = step,
            DefaultValue = createTagRequest.DefaultValue,
            OptionListId = createTagRequest.OptionListId,
            GroupId = createTagRequest.GroupId,
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
        var spec = new TagsForUserSpec(userId);
        var tags = await _tagRepository.GetAsync(spec);

        return tags.Select(MapTagToResponse).ToList();
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
        var spec = new TagByIdAndUserSpec(id, userId);
        var tag = await _tagRepository.GetSingleAsync(spec);

        if (tag == null)
        {
            throw new KeyNotFoundException("Tag not found");
        }

        // Apply InputType constraints for locked types
        var inputTypeId = model.TypeId == 0 ? (int?)null : model.TypeId;
        double? minValue = model.MinValue;
        double? maxValue = model.MaxValue;
        double? step = model.Step;
        bool isRepeatable = model.IsRepeatable;

        if (inputTypeId.HasValue)
        {
            var inputType = await _context.InputTypes.FindAsync(inputTypeId.Value);
            if (inputType != null)
            {
                // Enforce min/max/step constraints if range not editable
                if (!inputType.IsRangeEditable || !inputType.IsMinimumEditable || !inputType.IsMaximumEditable || !inputType.IsStepEditable)
                {
                    var constraints = InputTypeDefaults.GetConstraintsForType(inputTypeId.Value);
                    if (!inputType.IsMinimumEditable) minValue = constraints.MinValue;
                    if (!inputType.IsMaximumEditable) maxValue = constraints.MaxValue;
                    if (!inputType.IsStepEditable) step = constraints.Step;
                    _logger.LogInformation("Applied locked constraints for InputType {InputTypeId}: Min={Min}, Max={Max}, Step={Step}",
                        inputTypeId, minValue, maxValue, step);
                }

                // Enforce IsRepeatable constraint if not editable
                if (!inputType.IsRepeatableEditable)
                {
                    isRepeatable = false;
                    _logger.LogInformation("Applied locked IsRepeatable=false for InputType {InputTypeId}", inputTypeId);
                }
            }
        }

        tag.TagName = model.Tag;
        tag.InputTypeId = inputTypeId;
        tag.IsRequired = model.IsRequired; // Map IsRequired
        tag.IsRepeatable = isRepeatable;
        tag.TimeGranularity = model.TimeGranularity;
        tag.IsRange = model.IsRange;
        tag.UnitId = model.UnitId;
        tag.MinValue = minValue;
        tag.MaxValue = maxValue;
        tag.Step = step;
        tag.DefaultValue = model.DefaultValue;
        tag.OptionListId = model.OptionListId;
        tag.GroupId = model.GroupId;

        _context.Tags.Update(tag);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete tag by its id
    /// </summary>
    /// <param name="id"></param>
    public async Task Delete(int id, Guid userId)
    {
        var spec = new TagByIdAndUserSpec(id, userId);
        Tag? link = await _tagRepository.GetSingleAsync(spec);

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
        var spec = new TagByIdAndUserSpec(tagId, userId);
        Tag? tagResponse = await _tagRepository.GetSingleAsync(spec);

        if (tagResponse == null)
        {
            throw new KeyNotFoundException("Tag not found");
        }

        return MapTagToResponse(tagResponse);
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
        // Get paginated items
        var pagedSpec = new PagedTagsSpec(userId, pageNumber, pageSize, orderBy, filter, filterType);
        var items = await _tagRepository.GetAsync(pagedSpec);

        // Get total count
        var countSpec = new TagCountSpec(userId, filter, filterType);
        var totalCount = await _tagRepository.CountAsync(countSpec);

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
                OptionListName = t.OptionList?.Name,
                GroupId = t.Group?.Id,
                GroupName = t.Group?.Name
            }).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private static TagResponse MapTagToResponse(Tag tag)
    {
        return new TagResponse
        {
            Id = tag.Id,
            Title = tag.TagName,
            InputTypeId = tag.InputTypeId,
            TypeId = tag.InputTypeId,
            IsRequired = tag.IsRequired,
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
            OptionListName = tag.OptionList?.Name,
            GroupId = tag.Group?.Id,
            GroupName = tag.Group?.Name
        };
    }
}
