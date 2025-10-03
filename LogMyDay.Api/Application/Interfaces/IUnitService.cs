using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IUnitService
{
    Task<IEnumerable<UnitResponse>> GetAllAsync();

    Task<UnitResponse> GetByIdAsync(int id);

    Task<int> CreateAsync(UnitRequest request);

    Task UpdateAsync(int id, UnitRequest request);

    Task DeleteAsync(int id);

    Task<IEnumerable<QuantityResponse>> GetQuantitiesAsync();
}
