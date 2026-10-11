using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IRecordService
{
    Task<RecordResponse> GetById(int id, Guid userId);

    /// <summary>Saves the record and its values in one transaction and updates the rules that read
    /// its fields. Empty values are not stored; at least one value is required.</summary>
    Task<RecordResponse> Create(RecordRequest request, Guid userId);

    /// <summary>Saves the record's time and values: changing the time moves every value, a value
    /// left empty is removed. The record type cannot change.</summary>
    Task<RecordResponse> Update(int id, RecordRequest request, Guid userId);

    /// <summary>Deletes the record with all its values.</summary>
    Task Delete(int id, Guid userId);
}
