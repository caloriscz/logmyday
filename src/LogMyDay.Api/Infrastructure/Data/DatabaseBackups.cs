namespace LogMyDay.Api.Infrastructure.Data;

using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>A file to download. The temporary file behind Content is deleted once Content is disposed.</summary>
public sealed record DatabaseDownload(Stream Content, string FileName, string ContentType) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>Backup and restore of the database files themselves.</summary>
public interface IDatabaseBackups
{
    /// <summary>A copy of the user's database file, as it is at this moment.</summary>
    Task<DatabaseDownload> ExportUser(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the user's database with an uploaded copy of it. The upload is checked first: a
    /// LogMyDay user database, of this version or an older one, holding this user's rows only.
    /// An older one is migrated before it takes the place. What it replaces is kept as a snapshot.
    /// </summary>
    /// <exception cref="InvalidDataException">The upload is not a database that can be restored.</exception>
    Task RestoreUser(Guid userId, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// A zip archive of the whole installation: the platform database and every user's database,
    /// at the paths they have in the data folder. Unpacked into an empty data folder, LogMyDay
    /// runs on it.
    /// </summary>
    Task<DatabaseDownload> ExportInstallation(CancellationToken cancellationToken);
}

public sealed class DatabaseBackups : IDatabaseBackups
{
    /// <summary>The folder that holds an upload while it is checked, next to the platform database.</summary>
    public const string WorkFolderName = "restore-work";

    private const string SqliteContentType = "application/vnd.sqlite3";

    private readonly UserDatabases _databases;
    private readonly ILogger<DatabaseBackups> _logger;

    public DatabaseBackups(UserDatabases databases, ILogger<DatabaseBackups> logger)
    {
        _databases = databases;
        _logger = logger;
    }

    public Task<DatabaseDownload> ExportUser(Guid userId, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            _databases.Ensure(userId);

            var path = Path.Combine(Path.GetTempPath(), $"logmyday-user-{Guid.NewGuid():N}.db");
            try
            {
                UserDatabases.WriteCopy(_databases.GetConnectionString(userId), path);
            }
            catch
            {
                File.Delete(path);

                throw;
            }

            _logger.LogInformation("Exported the database of user {UserId}", userId);

            return new DatabaseDownload(OpenTemporary(path), $"logmyday-user-{Stamp()}.db", SqliteContentType);
        }, cancellationToken);
    }

    public async Task RestoreUser(Guid userId, Stream content, CancellationToken cancellationToken)
    {
        // Next to the databases, so that the checked file is moved into place, not copied.
        var workFolder = Path.Combine(_databases.Root, WorkFolderName);
        Directory.CreateDirectory(workFolder);
        var upload = Path.Combine(workFolder, $"{Guid.NewGuid():N}.db");

        try
        {
            await using (var file = new FileStream(upload, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await content.CopyToAsync(file, cancellationToken);
            }

            await Task.Run(() =>
            {
                Check(upload, userId);
                Migrate(upload, userId);
                _databases.Replace(userId, upload);
            }, cancellationToken);
        }
        finally
        {
            foreach (var file in new[] { upload, upload + "-wal", upload + "-shm", upload + "-journal" })
            {
                File.Delete(file);
            }
        }
    }

    public Task<DatabaseDownload> ExportInstallation(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var work = Path.Combine(Path.GetTempPath(), $"logmyday-installation-{Guid.NewGuid():N}");
            var archive = work + ".zip";
            try
            {
                Directory.CreateDirectory(Path.Combine(work, UserDatabases.FolderName));

                var platformPath = DatabaseLocation.GetFilePath(_databases.PlatformConnectionString)!;
                var platformCopy = Path.Combine(work, Path.GetFileName(platformPath));
                UserDatabases.WriteCopy(_databases.PlatformConnectionString, platformCopy);

                // The users of the copy, so that the archive holds a database for each account in it.
                var users = 0;
                foreach (var userId in ReadUserIds(platformCopy))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var source = _databases.GetFilePath(userId);
                    if (!File.Exists(source))
                    {
                        continue;
                    }

                    UserDatabases.WriteCopy(_databases.GetConnectionString(userId), Path.Combine(work, UserDatabases.FolderName, Path.GetFileName(source)));
                    users++;
                }

                ZipFile.CreateFromDirectory(work, archive, CompressionLevel.Fastest, includeBaseDirectory: false);

                _logger.LogInformation("Exported the installation: the platform database and {Users} user databases", users);

                return new DatabaseDownload(OpenTemporary(archive), $"logmyday-installation-{Stamp()}.zip", "application/zip");
            }
            catch
            {
                File.Delete(archive);

                throw;
            }
            finally
            {
                if (Directory.Exists(work))
                {
                    Directory.Delete(work, recursive: true);
                }
            }
        }, cancellationToken);
    }

    private static string Stamp() => DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);

    private static FileStream OpenTemporary(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
    }

    // No pooling: the file has to be free to move or delete as soon as the connection closes.
    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        connection.Open();

        return connection;
    }

    private static List<Guid> ReadUserIds(string platformPath)
    {
        using var connection = Open(platformPath);

        return ReadStrings(connection, "SELECT Id FROM LogMyDay_Users;").Select(Guid.Parse).ToList();
    }

    private static void Check(string path, Guid userId)
    {
        List<string> tables;
        List<string> applied;
        var foreignTables = new List<string>();
        try
        {
            using var connection = Open(path);

            if (ReadStrings(connection, "PRAGMA integrity_check;").FirstOrDefault() != "ok")
            {
                throw new InvalidDataException("The file is damaged: SQLite's integrity check fails on it.");
            }

            tables = ReadStrings(connection, "SELECT name FROM sqlite_master WHERE type = 'table';");
            if (tables.Contains("LogMyDay_Users"))
            {
                throw new InvalidDataException(
                    "The file holds user accounts: it is a platform database or the database of an older LogMyDay, not one user's database.");
            }

            if (!tables.Contains("LogMyDay_Activities") || !tables.Contains("__EFMigrationsHistory"))
            {
                throw new InvalidDataException("The file is not a LogMyDay user database.");
            }

            applied = ReadStrings(connection, "SELECT MigrationId FROM __EFMigrationsHistory;");

            foreach (var table in tables)
            {
                if (ReadStrings(connection, $"SELECT name FROM pragma_table_info('{table.Replace("'", "''")}') WHERE name = 'UserId';").Count == 0)
                {
                    continue;
                }

                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM \"{table.Replace("\"", "\"\"")}\" WHERE UserId IS NOT NULL AND UserId <> $user COLLATE NOCASE;";
                command.Parameters.AddWithValue("$user", userId.ToString("D"));
                if ((long)command.ExecuteScalar()! > 0)
                {
                    foreignTables.Add(table);
                }
            }
        }
        catch (SqliteException ex)
        {
            throw new InvalidDataException($"The file is not a SQLite database that LogMyDay can read: {ex.Message}", ex);
        }

        using var model = new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite("Data Source=:memory:").Options);
        var known = model.Database.GetMigrations().ToHashSet();

        var newer = applied.Where(m => !known.Contains(m)).ToList();
        if (newer.Count > 0)
        {
            throw new InvalidDataException(
                $"The backup comes from a newer LogMyDay than this one (it has {string.Join(", ", newer)}). Update LogMyDay, then restore it.");
        }

        if (applied.Count == 0)
        {
            throw new InvalidDataException("The file is not a LogMyDay user database: it records no schema version.");
        }

        if (foreignTables.Count > 0)
        {
            throw new InvalidDataException(
                "The file holds another user's data. A database file restores only into the account it was taken from; to move data between accounts, use the JSON backup.");
        }
    }

    /// <summary>Brings an older backup to this version's schema while it is still a work file.</summary>
    private void Migrate(string path, Guid userId)
    {
        var connectionString = new SqliteConnectionStringBuilder(_databases.GetConnectionString(userId)) { DataSource = path, Pooling = false }.ToString();
        using var context = new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite(connectionString).Options);

        try
        {
            context.Database.Migrate();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"The backup could not be brought to this version's schema: {ex.Message}", ex);
        }
    }

    private static List<string> ReadStrings(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var values = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
