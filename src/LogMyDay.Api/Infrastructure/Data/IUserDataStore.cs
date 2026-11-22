namespace LogMyDay.Api.Infrastructure.Data;

/// <summary>
/// Where the users' data is kept, as far as the account management needs to know: a new user
/// gets a place for their data, and a deleted user's data goes with the account.
/// </summary>
public interface IUserDataStore
{
    /// <summary>Prepares the store of a new user's data. Safe to call for a user who has one.</summary>
    void Create(Guid userId);

    /// <summary>Deletes everything the user logged. The account itself is not touched.</summary>
    void Delete(Guid userId);
}

/// <summary>
/// For hosts that keep all users in one store (the in-memory database of the integration tests):
/// there is nothing to create, and nothing is deleted.
/// </summary>
public sealed class SharedUserDataStore : IUserDataStore
{
    public void Create(Guid userId)
    {
    }

    public void Delete(Guid userId)
    {
    }
}
