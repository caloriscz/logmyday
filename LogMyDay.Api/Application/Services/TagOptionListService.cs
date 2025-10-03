using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace LogMyDay.Api.Application.Services;

public class TagOptionListService : ITagOptionListService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<TagOptionListService> _logger;

    public TagOptionListService(LogMyDayDbContext context, ILogger<TagOptionListService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<TagOptionListResponse>> GetAllAsync(Guid userId)
    {
        var lists = await _context.TagOptionLists
            .Include(l => l.Options)
            .Where(l => l.UserId == null || l.UserId == userId)
            .OrderBy(l => l.Name)
            .ToListAsync();

        return lists.Select(MapToResponse).ToList();
    }

    public async Task<TagOptionListResponse> GetByIdAsync(int id, Guid userId)
    {
        var list = await _context.TagOptionLists
            .Include(l => l.Options)
            .FirstOrDefaultAsync(l => l.Id == id && (l.UserId == null || l.UserId == userId));

        if (list == null)
        {
            throw new KeyNotFoundException("Option list not found");
        }

        return MapToResponse(list);
    }

    public async Task<int> CreateAsync(TagOptionListRequest request, Guid userId)
    {
        var list = new TagOptionList
        {
            Name = request.Name.Trim(),
            UserId = request.IsGlobal ? null : userId
        };

        foreach (var option in request.Options)
        {
            list.Options.Add(new TagOption
            {
                Value = option.Value.Trim(),
                DisplayName = option.DisplayName?.Trim()
            });
        }

        _context.TagOptionLists.Add(list);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created option list {ListId} for user {UserId}", list.Id, list.UserId ?? userId);

        return list.Id;
    }

    public async Task UpdateAsync(int id, TagOptionListRequest request, Guid userId)
    {
        var list = await _context.TagOptionLists
            .Include(l => l.Options)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (list == null)
        {
            throw new KeyNotFoundException("Option list not found");
        }

        if (list.UserId != userId)
        {
            throw new InvalidOperationException("Only personal option lists can be edited.");
        }

        list.Name = request.Name.Trim();

        var optionIds = request.Options.Where(o => o.Id.HasValue).Select(o => o.Id!.Value).ToHashSet();
        var toRemove = list.Options.Where(o => !optionIds.Contains(o.Id)).ToList();
        foreach (var option in toRemove)
        {
            _context.TagOptions.Remove(option);
        }

        foreach (var optionRequest in request.Options)
        {
            if (optionRequest.Id.HasValue)
            {
                var existing = list.Options.FirstOrDefault(o => o.Id == optionRequest.Id.Value);
                if (existing != null)
                {
                    existing.Value = optionRequest.Value.Trim();
                    existing.DisplayName = optionRequest.DisplayName?.Trim();
                }
            }
            else
            {
                list.Options.Add(new TagOption
                {
                    Value = optionRequest.Value.Trim(),
                    DisplayName = optionRequest.DisplayName?.Trim()
                });
            }
        }

        _context.TagOptionLists.Update(list);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated option list {ListId}", id);
    }

    public async Task DeleteAsync(int id, Guid userId)
    {
        var list = await _context.TagOptionLists.FirstOrDefaultAsync(l => l.Id == id);
        if (list == null)
        {
            return;
        }

        if (list.UserId != userId)
        {
            throw new InvalidOperationException("Only personal option lists can be deleted.");
        }

        var isInUse = await _context.Tags.AnyAsync(t => t.OptionListId == id);
        if (isInUse)
        {
            throw new InvalidOperationException("Option list is in use by one or more tags.");
        }

        _context.TagOptionLists.Remove(list);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted option list {ListId}", id);
    }

    private static TagOptionListResponse MapToResponse(TagOptionList list)
    {
        return new TagOptionListResponse
        {
            Id = list.Id,
            Name = list.Name,
            IsGlobal = list.UserId == null,
            Options = list.Options
                .OrderBy(o => o.DisplayName ?? o.Value)
                .Select(o => new TagOptionResponse
                {
                    Id = o.Id,
                    Value = o.Value,
                    DisplayName = o.DisplayName
                })
                .ToList()
        };
    }
}
