using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Api.Infrastructure.Specifications;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Services;

public class ColorSchemeService : IColorSchemeService
{
    private readonly IRepository<ColorScheme> _repository;

    public ColorSchemeService(IRepository<ColorScheme> repository)
    {
        _repository = repository;
    }

    public async Task<IList<ColorSchemeResponse>> GetAll(Guid userId)
    {
        var spec = new ColorSchemeSpecifications.ColorSchemesForUserSpec(userId);
        var schemes = await _repository.GetAsync(spec);

        return schemes
            .OrderBy(s => s.DisplayOrder ?? int.MaxValue)
            .ThenBy(s => s.Name)
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<ColorSchemeResponse> GetById(int id, Guid userId)
    {
        var spec = new ColorSchemeSpecifications.ColorSchemeByIdAndUserSpec(id, userId);
        var scheme = await _repository.GetSingleAsync(spec);

        if (scheme == null)
        {
            throw new KeyNotFoundException("Color scheme not found");
        }

        return MapToResponse(scheme);
    }

    public async Task<int> Create(ColorSchemeRequest request, Guid userId)
    {
        var scheme = new ColorScheme
        {
            Name = request.Name,
            UserId = userId,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            DateCreated = DateTime.UtcNow,
            Entries = request.Entries.Select(MapEntry).ToList()
        };

        await _repository.AddAsync(scheme);
        await _repository.SaveChangesAsync();

        return scheme.Id;
    }

    public async Task Update(int id, ColorSchemeRequest request, Guid userId)
    {
        var spec = new ColorSchemeSpecifications.ColorSchemeByIdAndUserSpec(id, userId);
        var scheme = await _repository.GetSingleAsync(spec);

        if (scheme == null)
        {
            throw new KeyNotFoundException("Color scheme not found");
        }

        scheme.Name = request.Name;
        scheme.Description = request.Description;
        scheme.DisplayOrder = request.DisplayOrder;

        // Replace entries wholesale; cascade delete removes the orphaned rows on save.
        scheme.Entries.Clear();
        foreach (var entry in request.Entries)
        {
            scheme.Entries.Add(MapEntry(entry));
        }

        await _repository.SaveChangesAsync();
    }

    public async Task Delete(int id, Guid userId)
    {
        var spec = new ColorSchemeSpecifications.ColorSchemeByIdAndUserSpec(id, userId);
        var scheme = await _repository.GetSingleAsync(spec);

        if (scheme == null)
        {
            throw new KeyNotFoundException("Color scheme not found");
        }

        await _repository.DeleteAsync(scheme);
        await _repository.SaveChangesAsync();
    }

    private static ColorSchemeEntry MapEntry(ColorSchemeEntryRequest entry) =>
        new()
        {
            RangeFrom = entry.RangeFrom,
            RangeTo = entry.RangeTo,
            Color = entry.Color,
            SortOrder = entry.SortOrder,
            Label = entry.Label
        };

    private static ColorSchemeResponse MapToResponse(ColorScheme scheme) =>
        new()
        {
            Id = scheme.Id,
            Name = scheme.Name,
            Description = scheme.Description,
            DisplayOrder = scheme.DisplayOrder,
            DateCreated = scheme.DateCreated,
            Entries = scheme.Entries
                .OrderBy(e => e.SortOrder)
                .Select(e => new ColorSchemeEntryResponse
                {
                    Id = e.Id,
                    RangeFrom = e.RangeFrom,
                    RangeTo = e.RangeTo,
                    Color = e.Color,
                    SortOrder = e.SortOrder,
                    Label = e.Label
                })
                .ToList()
        };
}
