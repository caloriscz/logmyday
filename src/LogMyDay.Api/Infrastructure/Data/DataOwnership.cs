namespace LogMyDay.Api.Infrastructure.Data;

using LogMyDay.Domain.Entities;

/// <summary>Where the rows of a table belong.</summary>
public enum DataOwner
{
    /// <summary>The installation: sign-in data. Lives in the platform database.</summary>
    Platform,

    /// <summary>A user, named by the row's UserId. Lives in that user's database.</summary>
    User,

    /// <summary>A user, through the parent row the entry names. Lives with the parent.</summary>
    Parent,

    /// <summary>Reference data (units, input types): every user's database has its own copy.</summary>
    Reference,

    /// <summary>
    /// Settings: the platform database holds the installation's settings, and each user's
    /// database holds the flags of its own one-time migrations.
    /// </summary>
    PlatformAndUser
}

/// <summary>How one entity is owned. <see cref="Parent"/> and <see cref="ParentKey"/> are set for <see cref="DataOwner.Parent"/>.</summary>
public sealed record DataOwnershipEntry(DataOwner Owner, Type? Parent = null, string? ParentKey = null);

/// <summary>
/// Says for every table whose it is. The split of an installation into the platform database and
/// one database per user copies rows by this map, and a test fails when an entity of either model
/// is missing here, so a new table can't be forgotten.
/// </summary>
public static class DataOwnership
{
    public static IReadOnlyDictionary<Type, DataOwnershipEntry> Map { get; } = new Dictionary<Type, DataOwnershipEntry>
    {
        [typeof(User)] = new(DataOwner.Platform),
        [typeof(ApiKey)] = new(DataOwner.Platform),
        [typeof(PasswordReset)] = new(DataOwner.Platform),

        [typeof(Setting)] = new(DataOwner.PlatformAndUser),

        [typeof(InputType)] = new(DataOwner.Reference),
        [typeof(Pattern)] = new(DataOwner.Reference),
        [typeof(Quantity)] = new(DataOwner.Reference),
        [typeof(Unit)] = new(DataOwner.Reference),

        [typeof(Activity)] = new(DataOwner.User),
        [typeof(ColorScheme)] = new(DataOwner.User),
        [typeof(EventLog)] = new(DataOwner.User),
        [typeof(Record)] = new(DataOwner.User),
        [typeof(Reminder)] = new(DataOwner.User),
        [typeof(ReminderDay)] = new(DataOwner.User),
        [typeof(ScanMapping)] = new(DataOwner.User),
        [typeof(Tag)] = new(DataOwner.User),
        [typeof(TagCheck)] = new(DataOwner.User),
        [typeof(TagDayLock)] = new(DataOwner.User),
        [typeof(TagGroup)] = new(DataOwner.User),
        [typeof(TagOptionList)] = new(DataOwner.User),
        [typeof(TagRule)] = new(DataOwner.User),
        [typeof(TagTrigger)] = new(DataOwner.User),
        [typeof(TodoList)] = new(DataOwner.User),

        [typeof(ColorSchemeEntry)] = new(DataOwner.Parent, typeof(ColorScheme), nameof(ColorSchemeEntry.ColorSchemeId)),
        [typeof(EventLogDetail)] = new(DataOwner.Parent, typeof(EventLog), nameof(EventLogDetail.EventLogId)),
        [typeof(TagOption)] = new(DataOwner.Parent, typeof(TagOptionList), nameof(TagOption.OptionListId)),
        [typeof(TagRuleCase)] = new(DataOwner.Parent, typeof(TagRule), nameof(TagRuleCase.RuleId)),
        [typeof(TagRuleSource)] = new(DataOwner.Parent, typeof(TagRule), nameof(TagRuleSource.RuleId)),
        [typeof(TodoItem)] = new(DataOwner.Parent, typeof(TodoList), nameof(TodoItem.ListId)),
    };
}
