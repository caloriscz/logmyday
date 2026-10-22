using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IRecordTypeService
{
    Task<IList<RecordTypeResponse>> GetAll(Guid userId);
    Task<RecordTypeResponse> GetById(int id, Guid userId);

    /// <summary>Creates the record type and a new tag for each field.</summary>
    Task<RecordTypeResponse> Create(RecordTypeRequest request, Guid userId);

    /// <summary>Saves the type and its fields: a field with a tag id is updated, one without is
    /// created, and a field left out is removed with its values (records left without a value go
    /// too). A field a rule reads cannot be removed.</summary>
    Task<RecordTypeResponse> Update(int id, RecordTypeRequest request, Guid userId);

    /// <summary>Deletes the type with its records, their values and its field tags. Refused while a
    /// rule reads one of its fields.</summary>
    Task Delete(int id, Guid userId);
}
