using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IUnitService
{
    Task<IEnumerable<UnitResponse>> GetAll();

    Task<UnitResponse> GetById(int id);

    Task<int> CreateAsync(UnitRequest request);

    Task Update(int id, UnitRequest request);

    Task Delete(int id);

    Task<IEnumerable<QuantityResponse>> GetQuantities();
}
