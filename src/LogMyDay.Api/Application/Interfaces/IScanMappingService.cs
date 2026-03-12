using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IScanMappingService
{
    Task<IList<ScanMappingResponse>> GetAll(Guid userId);
    Task<ScanMappingResponse> GetById(int id, Guid userId);
    Task<ScanLookupResponse> Lookup(string codeValue, Guid userId);
    Task<ScanMappingResponse> Create(ScanMappingRequest request, Guid userId);
    Task<ScanMappingResponse> Update(int id, ScanMappingRequest request, Guid userId);
    Task Delete(int id, Guid userId);
}
