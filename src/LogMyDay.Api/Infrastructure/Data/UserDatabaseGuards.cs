namespace LogMyDay.Api.Infrastructure.Data;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Refuses to save a row of another user into a user's database. Reads need no guard: a database
/// holds one user's rows, so a query for someone else finds nothing.
/// </summary>
public sealed class OwnerGuardInterceptor : SaveChangesInterceptor
{
    private readonly Guid _ownerId;

    public OwnerGuardInterceptor(Guid ownerId)
    {
        _ownerId = ownerId;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Check(DbContext? context)
    {
        if (context == null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var userId = entry.Metadata.FindProperty("UserId");
            if (userId == null || entry.Property(userId.Name).CurrentValue is not Guid rowOwner)
            {
                continue;
            }

            if (rowOwner != _ownerId)
            {
                throw new InvalidOperationException(
                    $"A {entry.Metadata.ClrType.Name} of user {rowOwner} can't be saved into the database of user {_ownerId}.");
            }
        }
    }
}

/// <summary>
/// Stops any use of a data context that was created for no user. Without it the context would
/// work on an empty in-memory database and hide the mistake.
/// </summary>
public sealed class NoUserInterceptor : DbConnectionInterceptor
{
    public const string Message =
        "No user's database is selected. Data is reached in a signed-in user's request, or in a scope opened for a user (IUserScopeFactory).";

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        throw new InvalidOperationException(Message);
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(Message);
    }
}
