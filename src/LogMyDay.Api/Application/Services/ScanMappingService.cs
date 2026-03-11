using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class ScanMappingService : IScanMappingService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<ScanMappingService> _logger;

    public ScanMappingService(LogMyDayDbContext context, ILogger<ScanMappingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IList<ScanMappingResponse>> GetAll(Guid userId)
    {
        var mappings = await _context.ScanMappings
            .AsNoTracking()
            .Include(s => s.Tag)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.DisplayName ?? s.Tag.TagName)
            .ToListAsync();

        return mappings.Select(MapToResponse).ToList();
    }

    public async Task<ScanMappingResponse> GetById(int id, Guid userId)
    {
        var mapping = await _context.ScanMappings
            .AsNoTracking()
            .Include(s => s.Tag)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (mapping == null)
        {
            throw new KeyNotFoundException("Scan mapping not found");
        }

        return MapToResponse(mapping);
    }

    public async Task<ScanLookupResponse> Lookup(string codeValue, Guid userId)
    {
        var mapping = await _context.ScanMappings
            .AsNoTracking()
            .Include(s => s.Tag)
                .ThenInclude(t => t.Unit)
            .Include(s => s.Tag)
                .ThenInclude(t => t.OptionList)
                    .ThenInclude(ol => ol!.Options)
            .FirstOrDefaultAsync(s => s.CodeValue == codeValue && s.UserId == userId && s.IsActive);

        if (mapping == null)
        {
            return new ScanLookupResponse { Found = false };
        }

        return new ScanLookupResponse
        {
            Found = true,
            Mapping = MapToResponse(mapping),
            Tag = MapTagToResponse(mapping.Tag)
        };
    }

    public async Task<ScanMappingResponse> Create(ScanMappingRequest request, Guid userId)
    {
        var tag = await _context.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TagId && t.UserId == userId);

        if (tag == null)
        {
            throw new KeyNotFoundException($"Tag with ID {request.TagId} not found");
        }

        var exists = await _context.ScanMappings
            .AnyAsync(s => s.UserId == userId && s.CodeValue == request.CodeValue);

        if (exists)
        {
            throw new InvalidOperationException($"A scan mapping for code '{request.CodeValue}' already exists");
        }

        var entity = new ScanMapping
        {
            UserId = userId,
            CodeValue = request.CodeValue,
            CodeType = request.CodeType,
            TagId = request.TagId,
            DisplayName = request.DisplayName,
            DefaultDescription = request.DefaultDescription,
            IsActive = request.IsActive,
            DateCreated = DateTime.UtcNow
        };

        _context.ScanMappings.Add(entity);
        await _context.SaveChangesAsync();

        entity.Tag = tag;

        return MapToResponse(entity);
    }

    public async Task<ScanMappingResponse> Update(int id, ScanMappingRequest request, Guid userId)
    {
        var mapping = await _context.ScanMappings
            .Include(s => s.Tag)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (mapping == null)
        {
            throw new KeyNotFoundException("Scan mapping not found");
        }

        if (mapping.CodeValue != request.CodeValue)
        {
            var exists = await _context.ScanMappings
                .AnyAsync(s => s.UserId == userId && s.CodeValue == request.CodeValue && s.Id != id);

            if (exists)
            {
                throw new InvalidOperationException($"A scan mapping for code '{request.CodeValue}' already exists");
            }
        }

        var tag = await _context.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TagId && t.UserId == userId);

        if (tag == null)
        {
            throw new KeyNotFoundException($"Tag with ID {request.TagId} not found");
        }

        mapping.CodeValue = request.CodeValue;
        mapping.CodeType = request.CodeType;
        mapping.TagId = request.TagId;
        mapping.DisplayName = request.DisplayName;
        mapping.DefaultDescription = request.DefaultDescription;
        mapping.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        mapping.Tag = tag;

        return MapToResponse(mapping);
    }

    public async Task Delete(int id, Guid userId)
    {
        var mapping = await _context.ScanMappings
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (mapping == null)
        {
            throw new KeyNotFoundException("Scan mapping not found");
        }

        _context.ScanMappings.Remove(mapping);
        await _context.SaveChangesAsync();
    }

    private static ScanMappingResponse MapToResponse(ScanMapping mapping)
    {
        return new ScanMappingResponse
        {
            Id = mapping.Id,
            CodeValue = mapping.CodeValue,
            CodeType = mapping.CodeType,
            TagId = mapping.TagId,
            TagName = mapping.Tag?.TagName,
            DisplayName = mapping.DisplayName,
            DefaultDescription = mapping.DefaultDescription,
            IsActive = mapping.IsActive,
            DateCreated = mapping.DateCreated
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
            OptionListName = tag.OptionList?.Name
        };
    }
}
