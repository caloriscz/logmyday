using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace LogMyDay.Api.Application.Services;

public class UnitService : IUnitService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<UnitService> _logger;

    public UnitService(LogMyDayDbContext context, ILogger<UnitService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<UnitResponse>> GetAllAsync()
    {
        var units = await _context.Units
            .Include(u => u.Quantity)
            .ThenInclude(q => q.BaseUnit)
            .OrderBy(u => u.Quantity.Key)
            .ThenBy(u => u.Key)
            .ToListAsync();

        return units.Select(MapUnitToResponse).ToList();
    }

    public async Task<UnitResponse> GetByIdAsync(int id)
    {
        var unit = await _context.Units
            .Include(u => u.Quantity)
            .ThenInclude(q => q.BaseUnit)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (unit == null)
        {
            throw new KeyNotFoundException("Unit not found");
        }

        return MapUnitToResponse(unit);
    }

    public async Task<int> CreateAsync(UnitRequest request)
    {
        await ValidateQuantityExists(request.QuantityId);
        await EnsureUniqueKeyAsync(request.Key, request.QuantityId);

        _logger.LogInformation("Creating unit {Key} for quantity {QuantityId}", request.Key, request.QuantityId);

        var unit = new Unit
        {
            Key = request.Key.Trim(),
            Symbol = request.Symbol.Trim(),
            QuantityId = request.QuantityId,
            AToBase = request.AToBase,
            BToBase = request.BToBase,
            Decimals = request.Decimals
        };

        _context.Units.Add(unit);
        await _context.SaveChangesAsync();

        return unit.Id;
    }

    public async Task UpdateAsync(int id, UnitRequest request)
    {
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == id);
        if (unit == null)
        {
            throw new KeyNotFoundException("Unit not found");
        }

        if (unit.QuantityId != request.QuantityId)
        {
            throw new InvalidOperationException("Changing a unit's quantity is not supported.");
        }

        await EnsureUniqueKeyAsync(request.Key, request.QuantityId, id);

        _logger.LogInformation("Updating unit {UnitId} with key {Key}", id, request.Key);

        unit.Key = request.Key.Trim();
        unit.Symbol = request.Symbol.Trim();
        unit.AToBase = request.AToBase;
        unit.BToBase = request.BToBase;
        unit.Decimals = request.Decimals;

        _context.Units.Update(unit);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var unit = await _context.Units
            .Include(u => u.Quantity)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (unit == null)
        {
            return;
        }

        if (unit.Quantity.BaseUnitId == id)
        {
            throw new InvalidOperationException("Cannot delete the base unit for a quantity.");
        }

        var isUsedByTags = await _context.Tags.AnyAsync(t => t.UnitId == id);
        if (isUsedByTags)
        {
            throw new InvalidOperationException("Unit is in use by one or more tags.");
        }

        _logger.LogInformation("Deleting unit {UnitId}", id);

        _context.Units.Remove(unit);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<QuantityResponse>> GetQuantitiesAsync()
    {
        var quantities = await _context.Quantities
            .Include(q => q.BaseUnit)
            .OrderBy(q => q.Key)
            .ToListAsync();

        return quantities.Select(q => new QuantityResponse
        {
            Id = q.Id,
            Key = q.Key,
            BaseUnitId = q.BaseUnitId,
            BaseUnitKey = q.BaseUnit?.Key ?? string.Empty,
            BaseUnitSymbol = q.BaseUnit?.Symbol ?? string.Empty
        }).ToList();
    }

    private static UnitResponse MapUnitToResponse(Unit unit)
    {
        var quantity = unit.Quantity ?? throw new InvalidOperationException("Unit does not have a quantity loaded.");

        return new UnitResponse
        {
            Id = unit.Id,
            Key = unit.Key,
            Symbol = unit.Symbol,
            QuantityId = unit.QuantityId,
            QuantityKey = quantity.Key,
            AToBase = unit.AToBase,
            BToBase = unit.BToBase,
            Decimals = unit.Decimals,
            IsBaseUnit = quantity.BaseUnitId == unit.Id
        };
    }

    private async Task ValidateQuantityExists(int quantityId)
    {
        var exists = await _context.Quantities.AnyAsync(q => q.Id == quantityId);
        if (!exists)
        {
            throw new KeyNotFoundException("Quantity not found");
        }
    }

    private async Task EnsureUniqueKeyAsync(string key, int quantityId, int? excludeId = null)
    {
        var normalizedKey = key.Trim();
        var query = _context.Units.Where(u => u.QuantityId == quantityId && u.Key == normalizedKey);

        if (excludeId.HasValue)
        {
            query = query.Where(u => u.Id != excludeId.Value);
        }

        var exists = await query.AnyAsync();
        if (exists)
        {
            throw new InvalidOperationException("A unit with the same key already exists for this quantity.");
        }
    }
}
