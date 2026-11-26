namespace LogMyDay.Api.Infrastructure.Data;

using System.Collections.Concurrent;
using LogMyDay.Api.Application.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// The one place that knows where a user's database is. The configured database file is the
/// platform database; each user's data is the file <c>users/&lt;User.Id&gt;.db</c> next to it.
/// </summary>
public sealed class UserDatabases : IUserDataStore
{
    /// <summary>The folder of the user databases, next to the platform database.</summary>
    public const string FolderName = "users";

    /// <summary>The database file itself, then the files SQLite keeps beside it, by their suffix.</summary>
    private static readonly string[] Companions = ["", "-wal", "-shm", "-journal"];

    private readonly SqliteConnectionStringBuilder _platform;
    private readonly string _folder;
    private readonly string _snapshotFolder;
    private readonly ILogger<UserDatabases> _logger;
    private readonly ConcurrentDictionary<Guid, bool> _ready = new();
    private readonly object _createLock = new();

    /// <param name="platformConnectionString">The connection string of the platform database, with an absolute file path.</param>
    public UserDatabases(string platformConnectionString, ILogger<UserDatabases> logger)
    {
        var path = DatabaseLocation.GetFilePath(platformConnectionString)
            ?? throw new NotSupportedException(
                "The database has to be a file: each user's data is a file of its own next to it.");

        _platform = new SqliteConnectionStringBuilder(platformConnectionString);
        var root = Path.GetDirectoryName(Path.GetFullPath(path))!;
        _folder = Path.Combine(root, FolderName);
        _snapshotFolder = Path.Combine(root, DatabaseUpgrade.SnapshotFolderName);
        _logger = logger;
    }

    /// <summary>The folder that holds the user databases.</summary>
    public string Folder => _folder;

    /// <summary>The folder that receives a database's snapshot before it is migrated.</summary>
    public string SnapshotFolder => _snapshotFolder;

    /// <summary>The connection string of the platform database.</summary>
    public string PlatformConnectionString => _platform.ConnectionString;

    /// <summary>The folder of the platform database, which holds everything of the installation.</summary>
    public string Root => Path.GetDirectoryName(_folder)!;

    /// <summary>The file of a user's database. It may not exist yet.</summary>
    public string GetFilePath(Guid userId)
    {
        return Path.Combine(_folder, userId.ToString("D") + ".db");
    }

    /// <summary>The platform's connection settings, pointed at the user's file.</summary>
    public string GetConnectionString(Guid userId)
    {
        return new SqliteConnectionStringBuilder(_platform.ConnectionString) { DataSource = GetFilePath(userId) }.ToString();
    }

    /// <summary>Points a data context at the user's database and makes it refuse other users' rows.</summary>
    public void Configure(DbContextOptionsBuilder options, Guid userId)
    {
        options
            .UseSqlite(GetConnectionString(userId))
            .AddInterceptors(new OwnerGuardInterceptor(userId));
    }

    /// <summary>
    /// Configures a data context for a scope that works for no user. Nothing can be read or
    /// written through it: there is no default database to fall back to.
    /// </summary>
    public static void ConfigureWithoutUser(DbContextOptionsBuilder options)
    {
        options
            .UseSqlite("Data Source=:memory:")
            .AddInterceptors(new NoUserInterceptor());
    }

    /// <summary>
    /// Makes sure the user's database exists with this version's schema and the reference data.
    /// A new user's database is created here, the first time it is needed; an existing one is
    /// migrated, after a snapshot. Refused for a user without an account.
    /// </summary>
    public void Ensure(Guid userId)
    {
        if (_ready.ContainsKey(userId))
        {
            return;
        }

        lock (_createLock)
        {
            if (_ready.ContainsKey(userId))
            {
                return;
            }

            // Only for an account: a deleted user's request must not bring their database back, nor
            // take up an empty file that a connection opened late, after the delete, left behind.
            if (!HasAccount(userId))
            {
                throw new InvalidOperationException($"There is no account {userId}, so its database is neither created nor opened.");
            }

            var isNew = !File.Exists(GetFilePath(userId));

            Directory.CreateDirectory(_folder);

            var options = new DbContextOptionsBuilder<LogMyDayDbContext>();
            Configure(options, userId);
            using var context = new LogMyDayDbContext(options.Options);

            DatabaseUpgrade.Migrate(context, _snapshotFolder, _logger);
            if (isNew)
            {
                OneTimeDataMigrations.MarkDone(context);
                _logger.LogInformation("Created the database of user {UserId}: {Path}", userId, GetFilePath(userId));
            }

            DefaultUnits.Ensure(context);

            _ready[userId] = true;
        }
    }

    /// <summary>Whether the platform database holds the user's account.</summary>
    private bool HasAccount(Guid userId)
    {
        var readOnly = new SqliteConnectionStringBuilder(_platform.ConnectionString) { Mode = SqliteOpenMode.ReadOnly };
        using var connection = new SqliteConnection(readOnly.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM LogMyDay_Users WHERE Id = $id COLLATE NOCASE;";
        command.Parameters.AddWithValue("$id", userId.ToString("D"));

        return (long)command.ExecuteScalar()! > 0;
    }

    /// <summary>Forgets that a user's database was prepared, e.g. after its file was replaced or removed.</summary>
    public void Forget(Guid userId)
    {
        _ready.TryRemove(userId, out _);
    }

    public void Create(Guid userId)
    {
        Ensure(userId);
    }

    /// <summary>Deletes the user's database file, with the files SQLite keeps beside it and its snapshots.</summary>
    public void Delete(Guid userId)
    {
        lock (_createLock)
        {
            // The account is gone by now: its snapshots go even when a file of the database is
            // still in use and can't be deleted yet.
            try
            {
                RemoveFiles(userId);
            }
            finally
            {
                SnapshotRetention.RemoveAll(_snapshotFolder, userId, _logger);
            }

            _logger.LogInformation("Deleted the database of user {UserId}: {Path}", userId, GetFilePath(userId));
        }
    }

    /// <summary>
    /// Puts another database file in the place of the user's database. The database it replaces
    /// is copied into the snapshot folder first, and its files wait aside until the new one is in
    /// place: when anything fails, they go back, so the user is never left without a database.
    /// The user keeps the newest snapshots only (<see cref="SnapshotRetention.PruneRestoreSnapshots"/>).
    /// </summary>
    /// <param name="newFile">A database with this version's schema, on the same volume. It is moved.</param>
    /// <returns>The snapshot of the replaced database, or null when the user had none.</returns>
    /// <exception cref="DatabaseNotReplacedException">The database was not replaced. The message says what the user has now.</exception>
    public string? Replace(Guid userId, string newFile)
    {
        lock (_createLock)
        {
            var path = GetFilePath(userId);
            string? snapshot = null;
            if (File.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(_snapshotFolder);
                    var target = SnapshotRetention.NewPath(_snapshotFolder, userId.ToString("D"), SnapshotRetention.BeforeRestore);
                    SnapshotRetention.Write(target, () => WriteCopy(GetConnectionString(userId), target));
                    snapshot = target;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
                {
                    _logger.LogWarning(ex, "The database of user {UserId} was not replaced: its snapshot could not be written", userId);

                    throw new DatabaseNotReplacedException(
                        "Your database was not replaced, because a snapshot of it could not be written first. Nothing was changed.", true, ex);
                }
            }

            var aside = Path.Combine(_folder, $"{userId:D}-{Guid.NewGuid():N}.previous");
            var movedAside = new List<string>();
            var replaced = false;
            try
            {
                Release(userId);
                foreach (var companion in Companions)
                {
                    if (File.Exists(path + companion))
                    {
                        File.Move(path + companion, aside + companion);
                        movedAside.Add(companion);
                    }
                }

                Directory.CreateDirectory(_folder);
                File.Move(newFile, path);
                replaced = true;
                Ensure(userId);
            }
            catch (Exception ex)
            {
                throw PutBack(userId, aside, movedAside, replaced, snapshot, ex);
            }

            foreach (var companion in movedAside)
            {
                DeleteAside(aside + companion);
            }

            _logger.LogInformation("Replaced the database of user {UserId}: {Path}. Its previous state: {Snapshot}", userId, path, snapshot ?? "none");

            // Only now: after a failed restore, the user's database may already hold the uploaded
            // state, and a retry's snapshot would push out the one of their real data.
            if (snapshot != null)
            {
                SnapshotRetention.PruneRestoreSnapshots(_snapshotFolder, userId, snapshot, _logger);
            }

            return snapshot;
        }
    }

    /// <summary>
    /// After a failed replace: takes out the new database if it got in, and puts the user's own
    /// files back where they were.
    /// </summary>
    /// <returns>The error to report, saying what the user has now.</returns>
    private DatabaseNotReplacedException PutBack(Guid userId, string aside, List<string> movedAside, bool replaced, string? snapshot, Exception failure)
    {
        var path = GetFilePath(userId);
        try
        {
            Release(userId);
            if (replaced)
            {
                foreach (var companion in Companions)
                {
                    File.Delete(path + companion);
                }
            }

            // Overwrite: a request that came in meanwhile may have created an empty file there.
            foreach (var companion in movedAside)
            {
                File.Move(aside + companion, path + companion, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogCritical(failure,
                "The database of user {UserId} was not replaced, and its previous files could not be put back ({PutBackError}). "
                + "They are {Aside} with the companions {Companions}, and the snapshot {Snapshot}. Move them back to {Path} with the application stopped.",
                userId, ex.Message, aside, string.Join(", ", movedAside), snapshot ?? "none", path);

            return new DatabaseNotReplacedException(
                "The restored database could not be put in place, and your previous database could not be put back. "
                + "It is kept on the server; an administrator can put it back.", false, failure);
        }

        _logger.LogWarning(failure, "The database of user {UserId} could not be replaced; its previous files are back in place", userId);

        return new DatabaseNotReplacedException(
            "Your database could not be replaced right now, so it was left as it was. Try again in a moment.", true, failure);
    }

    /// <summary>The previous files are no longer needed once the new database is in place; the snapshot keeps their content.</summary>
    private void DeleteAside(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The replaced database file {File} could not be deleted; delete it by hand", file);
        }
    }

    /// <summary>
    /// Writes a consistent copy of a database into a new file, whatever is still in the
    /// write-ahead log.
    /// </summary>
    public static void WriteCopy(string connectionString, string targetPath)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $target;";
        command.Parameters.AddWithValue("$target", targetPath);
        command.ExecuteNonQuery();
    }

    private void RemoveFiles(Guid userId)
    {
        Release(userId);

        var path = GetFilePath(userId);
        foreach (var companion in Companions)
        {
            File.Delete(path + companion);
        }
    }

    /// <summary>Lets go of the user's files, so that they can be moved or deleted.</summary>
    private void Release(Guid userId)
    {
        Forget(userId);

        // Pooled connections keep the file open.
        using var connection = new SqliteConnection(GetConnectionString(userId));
        SqliteConnection.ClearPool(connection);
    }
}
