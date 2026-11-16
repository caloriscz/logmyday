using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

/// <summary>Record types (with their fields) and records.</summary>
public interface IRecordApi
{
    [Get("/api/record-types")]
    Task<IList<RecordTypeResponse>> GetRecordTypes();

    [Get("/api/record-types/{id}")]
    Task<RecordTypeResponse> GetRecordTypeById(int id);

    [Post("/api/record-types")]
    Task<RecordTypeResponse> CreateRecordType([Body] RecordTypeRequest request);

    [Put("/api/record-types/{id}")]
    Task<RecordTypeResponse> UpdateRecordType(int id, [Body] RecordTypeRequest request);

    [Delete("/api/record-types/{id}")]
    Task DeleteRecordType(int id);

    /// <summary>Records, newest first, optionally of one record type and between two days
    /// (yyyy-MM-dd, inclusive).</summary>
    [Get("/api/records")]
    Task<PagedResult<RecordResponse>> GetRecords(int? recordTypeId = null, string? from = null, string? to = null, int page = 1, int pageSize = 50);

    [Get("/api/records/{id}")]
    Task<RecordResponse> GetRecordById(int id);

    [Post("/api/records")]
    Task<RecordResponse> CreateRecord([Body] RecordRequest request);

    [Put("/api/records/{id}")]
    Task<RecordResponse> UpdateRecord(int id, [Body] RecordRequest request);

    [Delete("/api/records/{id}")]
    Task DeleteRecord(int id);
}
