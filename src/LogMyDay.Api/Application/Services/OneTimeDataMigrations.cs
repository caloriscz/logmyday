using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// The one-time migrations of a user's data that run at startup, known by the flags they leave in
/// the user's database.
/// </summary>
public static class OneTimeDataMigrations
{
    /// <summary>The flags of <see cref="ReminderDayBackfill"/> and <see cref="ReminderRecurrenceMigration"/>.</summary>
    public static readonly IReadOnlyList<string> FlagKeys = [ReminderDayBackfill.FlagKey, ReminderRecurrenceMigration.FlagKey];

    /// <summary>
    /// Marks them as done in a database this version has just created. It holds no data of an
    /// older version to convert, and running them over the data written since would duplicate it.
    /// </summary>
    public static void MarkDone(LogMyDayDbContext context)
    {
        foreach (var key in FlagKeys)
        {
            context.Settings.Add(new Setting
            {
                Key = key,
                Value = "done",
                Description = "Not needed: the database was created by this version"
            });
        }

        context.SaveChanges();
    }
}
