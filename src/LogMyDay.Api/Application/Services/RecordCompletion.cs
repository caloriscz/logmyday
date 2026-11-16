using LogMyDay.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

/// <summary>A reminder or todo whose completion target is a record type completes through the
/// record form: the record is saved first and the completion names it. The record must be of
/// that type and from the completion's local day, so an old record cannot complete today.
/// Completing logs nothing of its own, and a skip writes no placeholder record.</summary>
internal static class RecordCompletion
{
    public static async Task EnsureLogged(LogMyDayDbContext context, int? recordId, int recordTypeId, string recordTypeName, DateOnly day, Guid userId)
    {
        var dayStart = day.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        if (recordId is not int id
            || !await context.Records.AnyAsync(r => r.Id == id && r.UserId == userId && r.RecordTypeId == recordTypeId
                && r.DateStarted >= dayStart && r.DateStarted < dayEnd))
        {
            throw new ArgumentException($"'{recordTypeName}' is a record type: log its record for {day:yyyy-MM-dd} first, then complete with that record's id.");
        }
    }
}
