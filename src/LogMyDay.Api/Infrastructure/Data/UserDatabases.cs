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
    /// migrated, after a snapshot.
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

            Directory.CreateDirectory(_folder);

            var options = new DbContextOptionsBuilder<LogMyDayDbContext>();
            Configure(options, userId);
            using var context = new LogMyDayDbContext(options.Options);

            var isNew = !File.Exists(GetFilePath(userId));
            DatabaseUpgrade.Migrate(context, _snapshotFolder, _logger);
            if (isNew)
            {
                OneTimeDataMigrations.MarkDone(context);
                _logger.LogInformation("Created the database of user {UserId}: {Path}", userId, GetFilePath(userId));
            }

            DefaultUnits.Ensure(context).GetAwaiter().GetResult();

            _ready[userId] = true;
        }
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

    /// <summary>Deletes the user's database file, with the files SQLite keeps beside it.</summary>
    public void Delete(Guid userId)
    {
        lock (_createLock)
        {
            RemoveFiles(userId);

            _logger.LogInformation("Deleted the database of user {UserId}: {Path}", userId, GetFilePath(userId));
        }
    }

    /// <summary>
    /// Puts another database file in the place of the user's database. The database it replaces
    /// is copied into the snapshot folder first.
    /// </summary>
    /// <param name="newFile">A database with this version's schema, on the same volume. It is moved.</param>
    /// <returns>The snapshot of the replaced database, or null when the user had none.</returns>
    public string? Replace(Guid userId, string newFile)
    {
        lock (_createLock)
        {
            var path = GetFilePath(userId);
            string? snapshot = null;
            if (File.Exists(path))
            {
                Directory.CreateDirectory(_snapshotFolder);
                snapshot = Path.Combine(_snapshotFolder, $"{userId:D}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-before-restore.db");
                WriteCopy(GetConnectionString(userId), snapshot);
            }

            RemoveFiles(userId);
            Directory.CreateDirectory(_folder);
            File.Move(newFile, path);
            Ensure(userId);

            _logger.LogInformation("Replaced the database of user {UserId}: {Path}. Its previous state: {Snapshot}", userId, path, snapshot ?? "none");

            return snapshot;
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
        Forget(userId);

        // Pooled connections keep the file open.
        using (var connection = new SqliteConnection(GetConnectionString(userId)))
        {
            SqliteConnection.ClearPool(connection);
        }

        var path = GetFilePath(userId);
        foreach (var file in new[] { path, path + "-wal", path + "-shm", path + "-journal" })
        {
            File.Delete(file);
        }
    }
}
