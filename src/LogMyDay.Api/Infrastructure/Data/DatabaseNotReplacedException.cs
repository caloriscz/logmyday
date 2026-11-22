namespace LogMyDay.Api.Infrastructure.Data;

/// <summary>
/// A user's database could not be replaced. The message says, for the user, what state their
/// database was left in.
/// </summary>
public sealed class DatabaseNotReplacedException : IOException
{
    public DatabaseNotReplacedException(string message, bool databaseUnchanged, Exception innerException)
        : base(message, innerException)
    {
        DatabaseUnchanged = databaseUnchanged;
    }

    /// <summary>True when the user's database is the one they had before; false when it could not be put back.</summary>
    public bool DatabaseUnchanged { get; }
}
