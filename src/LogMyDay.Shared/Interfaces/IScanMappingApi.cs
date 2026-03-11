using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface IScanMappingApi
{
    [Get("/api/scan-mappings")]
    Task<IList<ScanMappingResponse>> GetAll();

    [Get("/api/scan-mappings/{id}")]
    Task<ScanMappingResponse> GetById(int id);

    [Get("/api/scan-mappings/lookup")]
    Task<ScanLookupResponse> Lookup([Query] string codeValue);

    [Post("/api/scan-mappings")]
    Task<ScanMappingResponse> Create([Body] ScanMappingRequest request);

    [Put("/api/scan-mappings/{id}")]
    Task<ScanMappingResponse> Update(int id, [Body] ScanMappingRequest request);

    [Delete("/api/scan-mappings/{id}")]
    Task Delete(int id);
}
