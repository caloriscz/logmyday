using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Api.Infrastructure.Specifications;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using static LogMyDay.Api.Infrastructure.Specifications.UnitSpecifications;

namespace LogMyDay.Api.Application.Services;

public class UnitService : IUnitService
{
    private readonly LogMyDayDbContext _context;
    private readonly IUnitRepository _unitRepository;
    private readonly IQuantityRepository _quantityRepository;
    private readonly ITagRepository _tagRepository;
    private readonly ILogger<UnitService> _logger;

    public UnitService(
        LogMyDayDbContext context,
        IUnitRepository unitRepository,
        IQuantityRepository quantityRepository,
        ITagRepository tagRepository,
        ILogger<UnitService> logger)
    {
        _context = context;
        _unitRepository = unitRepository;
        _quantityRepository = quantityRepository;
        _tagRepository = tagRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<UnitResponse>> GetAll()
    {
        var spec = new UnitsWithQuantitySpec();
        var units = await _unitRepository.GetAsync(spec);

        return units.Select(MapUnitToResponse).ToList();
    }

    public async Task<UnitResponse> GetById(int id)
    {
        var spec = new UnitByIdSpec(id);
        var unit = await _unitRepository.GetSingleAsync(spec);

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

    public async Task Update(int id, UnitRequest request)
    {
        var spec = new UnitByIdSpec(id);
        var unit = await _unitRepository.GetSingleAsync(spec);
        
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

    public async Task Delete(int id)
    {
        var spec = new UnitWithQuantityForDeleteSpec(id);
        var unit = await _unitRepository.GetSingleAsync(spec);

        if (unit == null)
        {
            return;
        }

        if (unit.Quantity?.BaseUnitId == id)
        {
            throw new InvalidOperationException("Cannot delete the base unit for a quantity.");
        }

        var tagSpec = new TagsUsingUnitSpec(id);
        var isUsedByTags = await _tagRepository.AnyAsync(tagSpec);
        if (isUsedByTags)
        {
            throw new InvalidOperationException("Unit is in use by one or more tags.");
        }

        _logger.LogInformation("Deleting unit {UnitId}", id);

        _context.Units.Remove(unit);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<QuantityResponse>> GetQuantities()
    {
        var spec = new QuantitiesWithBaseUnitSpec();
        var quantities = await _quantityRepository.GetAsync(spec);

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
        var spec = new QuantityByIdSpec(quantityId);
        var exists = await _quantityRepository.AnyAsync(spec);
        if (!exists)
        {
            throw new KeyNotFoundException("Quantity not found");
        }
    }

    private async Task EnsureUniqueKeyAsync(string key, int quantityId, int? excludeId = null)
    {
        var normalizedKey = key.Trim();
        var spec = new UnitByKeyAndQuantitySpec(normalizedKey, quantityId, excludeId);
        var exists = await _unitRepository.AnyAsync(spec);
        
        if (exists)
        {
            throw new InvalidOperationException("A unit with the same key already exists for this quantity.");
        }
    }
}
