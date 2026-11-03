using LogMyDay.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

/// <summary>A reminder or todo whose completion target is a record type completes through the
/// record form: the record is saved first and the completion names it. Completing logs nothing
/// of its own, and a skip writes no placeholder record.</summary>
internal static class RecordCompletion
{
    public static async Task EnsureLogged(LogMyDayDbContext context, int? recordId, int recordTypeId, string recordTypeName, Guid userId)
    {
        if (recordId is not int id
            || !await context.Records.AnyAsync(r => r.Id == id && r.UserId == userId && r.RecordTypeId == recordTypeId))
        {
            throw new ArgumentException($"'{recordTypeName}' is a record type: log the record first, then complete with its record id.");
        }
    }
}
