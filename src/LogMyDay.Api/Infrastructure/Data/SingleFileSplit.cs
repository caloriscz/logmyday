namespace LogMyDay.Api.Infrastructure.Data;

using LogMyDay.Api.Application.Services;
using LogMyDay.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

/// <summary>What a split did. The counts are rows per table.</summary>
/// <param name="BackupPath">Where the original single file is kept.</param>
/// <param name="Users">The users that got a database.</param>
/// <param name="SourceRows">Rows in the original file.</param>
/// <param name="CopiedRows">Rows in the new files, all of them together.</param>
/// <param name="LeftBehindRows">Rows that belong to no user. They stay in the backup only.</param>
public sealed record SplitReport(
    string BackupPath,
    IReadOnlyList<Guid> Users,
    IReadOnlyDictionary<string, long> SourceRows,
    IReadOnlyDictionary<string, long> CopiedRows,
    IReadOnlyDictionary<string, long> LeftBehindRows);

/// <summary>
/// Upgrades an installation of v0.7.2, which kept everything in one file, to the platform
/// database and one database per user. The new files are built aside and take their places only
/// when every row is accounted for; the original file is kept under a backup name. On any error
/// nothing changes.
/// </summary>
public sealed class SingleFileSplit
{
    /// <summary>The last migration of v0.7.2. Only a database at this migration can be split.</summary>
    public const string LastSingleFileMigration = "20261116190252_AddTagTriggers";

    /// <summary>Goes between the name and the extension of the original file's backup.</summary>
    public const string BackupSuffix = ".before-v0.8.0";

    private const string WorkFolderName = "split-work";

    // A user's database keeps the flags of its own one-time migrations; the rest of the settings
    // belong to the installation.
    private static readonly IReadOnlyList<string> UserSettingKeys = OneTimeDataMigrations.FlagKeys;

    private readonly SqliteConnectionStringBuilder _platform;
    private readonly UserDatabases _databases;
    private readonly ILogger<SingleFileSplit> _logger;
    private readonly string _sourcePath;
    private readonly string _root;

    /// <param name="platformConnectionString">The connection string of the configured database, with an absolute file path.</param>
    public SingleFileSplit(string platformConnectionString, UserDatabases databases, ILogger<SingleFileSplit> logger)
    {
        _platform = new SqliteConnectionStringBuilder(platformConnectionString);
        _databases = databases;
        _logger = logger;
        _sourcePath = Path.GetFullPath(DatabaseLocation.GetFilePath(platformConnectionString)
            ?? throw new NotSupportedException("The database has to be a file."));
        _root = Path.GetDirectoryName(_sourcePath)!;
    }

    /// <summary>Where the original file is kept after the split.</summary>
    public string BackupPath => Path.Combine(_root, Path.GetFileNameWithoutExtension(_sourcePath) + BackupSuffix + Path.GetExtension(_sourcePath));

    /// <summary>
    /// Splits the configured database when it is a single file of v0.7.2, and returns what was
    /// done. Returns null when there is nothing to split.
    /// </summary>
    public SplitReport? SplitIfNeeded()
    {
        if (!File.Exists(_sourcePath))
        {
            if (File.Exists(BackupPath))
            {
                // A split was interrupted between moving the original away and putting the new
                // platform database in its place. Starting now would create an empty installation.
                throw new InvalidOperationException(
                    $"The database {_sourcePath} is missing, but its backup {BackupPath} exists: an upgrade was interrupted. "
                    + $"Rename the backup back to {Path.GetFileName(_sourcePath)}, remove the folder {_databases.Folder}, and start again.");
            }

            return null;
        }

        if (!IsSingleFile())
        {
            return null;
        }

        _logger.LogInformation("The database {Path} holds every user's data in one file. Splitting it into the platform database and one database per user", _sourcePath);

        CheckBeforeSplit();

        var work = Path.Combine(_root, WorkFolderName);
        DeleteFolder(work);
        try
        {
            Directory.CreateDirectory(Path.Combine(work, UserDatabases.FolderName));

            var report = Build(work);
            PutInPlace(work);
            DeleteFolder(work);

            _logger.LogInformation("Split done: {Users} user databases in {Folder}. The original file is kept as {Backup}",
                report.Users.Count, _databases.Folder, report.BackupPath);

            return report;
        }
        catch (Exception ex) when (File.Exists(_sourcePath))
        {
            // The original is still in its place, so nothing changed for the installation.
            SqliteConnection.ClearAllPools();
            DeleteFolder(work);

            throw new InvalidOperationException($"The database {_sourcePath} could not be split, and it was left as it is. {ex.Message}", ex);
        }
    }

    /// <summary>A single-file database has the users' data tables next to the user accounts.</summary>
    private bool IsSingleFile()
    {
        using var source = Open(_sourcePath, SqliteOpenMode.ReadOnly);

        return Scalar(source, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('LogMyDay_Users', 'LogMyDay_Activities');") == 2;
    }

    private void CheckBeforeSplit()
    {
        using (var source = Open(_sourcePath, SqliteOpenMode.ReadWrite))
        {
            // Whatever is still in the write-ahead log belongs into the file that becomes the backup.
            Execute(source, "PRAGMA wal_checkpoint(TRUNCATE);");

            var hasHistory = Scalar(source, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory';") == 1;
            if (!hasHistory || Scalar(source, "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = $id;", ("$id", LastSingleFileMigration)) == 0)
            {
                throw new InvalidOperationException(
                    $"The database {_sourcePath} is older than LogMyDay v0.7.2 (it lacks the migration {LastSingleFileMigration}). "
                    + "Install v0.7.2 first, let it update the database, and then install this version.");
            }
        }

        if (File.Exists(BackupPath))
        {
            throw new InvalidOperationException($"The backup {BackupPath} already exists. Move it away to split {_sourcePath} again.");
        }

        if (Directory.Exists(_databases.Folder) && Directory.EnumerateFileSystemEntries(_databases.Folder).Any())
        {
            throw new InvalidOperationException(
                $"The folder {_databases.Folder} already holds files, while {_sourcePath} still holds every user's data. Remove the folder to split the database.");
        }
    }

    private SplitReport Build(string work)
    {
        var platformPath = Path.Combine(work, Path.GetFileName(_sourcePath));
        var sourceRows = new Dictionary<string, long>();
        var copiedRows = new Dictionary<string, long>();
        var leftBehind = new Dictionary<string, long>();

        IModel platformModel;
        using (var platform = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(ConnectionString(platformPath)).Options))
        {
            DatabaseUpgrade.Migrate(platform, _databases.SnapshotFolder, _logger);
            platformModel = platform.Model;
        }

        List<(Guid Id, string Stored)> users;
        string? heir;
        using (var target = OpenWithSource(platformPath))
        {
            Execute(target, "BEGIN;");
            foreach (var entityType in platformModel.GetEntityTypes())
            {
                var table = entityType.GetTableName()!;
                var filter = entityType.ClrType == typeof(Setting) ? $"WHERE \"Key\" NOT IN ({Quoted(UserSettingKeys)})" : string.Empty;

                sourceRows[table] = Scalar(target, $"SELECT COUNT(*) FROM src.\"{table}\" {filter};");
                copiedRows[table] = Copy(target, entityType, filter);
                Expect(table, sourceRows[table], copiedRows[table]);
            }

            Execute(target, "COMMIT;");

            users = ReadStrings(target, "SELECT Id FROM main.LogMyDay_Users ORDER BY CreatedUtc, Id;")
                .Select(id => (Guid.Parse(id), id))
                .ToList();

            // Option lists that nobody uses need an owner: the first admin, or else the first user.
            heir = ReadStrings(target, "SELECT Id FROM main.LogMyDay_Users ORDER BY IsAdmin DESC, CreatedUtc, Id LIMIT 1;").FirstOrDefault();
        }

        IModel? dataModel = null;
        foreach (var user in users)
        {
            var path = Path.Combine(work, UserDatabases.FolderName, Path.GetFileName(_databases.GetFilePath(user.Id)));
            using (var data = new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite(ConnectionString(path)).Options))
            {
                DatabaseUpgrade.Migrate(data, _databases.SnapshotFolder, _logger);
                dataModel = data.Model;
            }

            using var target = OpenWithSource(path);
            CopyUser(target, dataModel, user.Stored, user.Stored == heir, copiedRows);
        }

        if (dataModel != null)
        {
            using var source = Open(_sourcePath, SqliteOpenMode.ReadOnly);
            Account(source, dataModel, users.Count, sourceRows, copiedRows, leftBehind);
        }

        return new SplitReport(BackupPath, users.Select(u => u.Id).ToList(), sourceRows, copiedRows, leftBehind);
    }

    private void CopyUser(SqliteConnection target, IModel model, string userId, bool isHeir, Dictionary<string, long> copiedRows)
    {
        Execute(target, "BEGIN;");

        // Parents come before the rows that follow them, and tags before the option lists, which
        // are chosen by the tags that use them.
        var ordered = model.GetEntityTypes()
            .OrderBy(t => DataOwnership.Map[t.ClrType].Owner == DataOwner.Parent ? 2 : t.ClrType == typeof(TagOptionList) ? 1 : 0)
            .ToList();

        foreach (var entityType in ordered)
        {
            var table = entityType.GetTableName()!;
            var ownership = DataOwnership.Map[entityType.ClrType];

            // The migrations seed the reference rows; the user gets the installation's rows instead.
            Execute(target, $"DELETE FROM main.\"{table}\";");

            var filter = ownership.Owner switch
            {
                DataOwner.Reference => string.Empty,
                DataOwner.PlatformAndUser => $"WHERE \"Key\" IN ({Quoted(UserSettingKeys)})",
                DataOwner.User when entityType.ClrType == typeof(TagOptionList) => OptionListFilter(isHeir),
                DataOwner.User => "WHERE UserId = $user",
                DataOwner.Parent => ParentFilter(model, entityType, ownership),
                _ => throw new NotSupportedException($"{entityType.DisplayName()} doesn't belong into a user's database.")
            };

            var copied = Copy(target, entityType, filter, ("$user", userId));
            copiedRows[table] = copiedRows.GetValueOrDefault(table) + copied;
        }

        // A list without an owner was shared by every user. Each user who needs it has a copy now.
        Execute(target, "UPDATE main.LogMyDay_TagOptionLists SET UserId = $user WHERE UserId IS NULL;", ("$user", userId));

        Execute(target, "COMMIT;");

        var broken = ReadStrings(target, "PRAGMA main.foreign_key_check;");
        foreach (var table in broken.GroupBy(t => t))
        {
            _logger.LogWarning("User {UserId}: {Count} rows of {Table} point at rows that aren't in the user's database", userId, table.Count(), table.Key);
        }
    }

    private static string OptionListFilter(bool isHeir)
    {
        var used = "\"UserId\" IS NULL AND \"Id\" IN (SELECT OptionListId FROM main.LogMyDay_Tags WHERE OptionListId IS NOT NULL)";
        var unused = "\"UserId\" IS NULL AND \"Id\" NOT IN (SELECT OptionListId FROM src.LogMyDay_Tags WHERE OptionListId IS NOT NULL AND UserId IN (SELECT Id FROM src.LogMyDay_Users))";

        return isHeir
            ? $"WHERE \"UserId\" = $user OR ({used}) OR ({unused})"
            : $"WHERE \"UserId\" = $user OR ({used})";
    }

    private static string ParentFilter(IModel model, IEntityType entityType, DataOwnershipEntry ownership)
    {
        var parent = model.FindEntityType(ownership.Parent!)!;
        var key = entityType.FindProperty(ownership.ParentKey!)!.GetColumnName();
        var parentKey = parent.FindPrimaryKey()!.Properties.Single().GetColumnName();

        return $"WHERE \"{key}\" IN (SELECT \"{parentKey}\" FROM main.\"{parent.GetTableName()}\")";
    }

    /// <summary>
    /// Compares the original file with the new ones, table by table. Every row has to be in a
    /// user's database or be counted as belonging to no user.
    /// </summary>
    private void Account(SqliteConnection source, IModel model, int userCount, Dictionary<string, long> sourceRows, Dictionary<string, long> copiedRows, Dictionary<string, long> leftBehind)
    {
        const string KnownUser = "UserId IN (SELECT Id FROM LogMyDay_Users)";

        foreach (var entityType in model.GetEntityTypes())
        {
            var table = entityType.GetTableName()!;
            var ownership = DataOwnership.Map[entityType.ClrType];
            if (ownership.Owner == DataOwner.PlatformAndUser)
            {
                continue;
            }

            var total = Scalar(source, $"SELECT COUNT(*) FROM \"{table}\";");
            sourceRows[table] = total;
            var copied = copiedRows.GetValueOrDefault(table);

            if (ownership.Owner == DataOwner.Reference)
            {
                Expect(table, total * userCount, copied);

                continue;
            }

            var isOptionList = entityType.ClrType == typeof(TagOptionList) || ownership.Parent == typeof(TagOptionList);
            long owned;
            if (ownership.Owner == DataOwner.User)
            {
                // A shared option list has no owner; it is never left behind.
                owned = Scalar(source, $"SELECT COUNT(*) FROM \"{table}\" WHERE {KnownUser}{(isOptionList ? " OR UserId IS NULL" : string.Empty)};");
            }
            else
            {
                var parent = model.FindEntityType(ownership.Parent!)!;
                var key = entityType.FindProperty(ownership.ParentKey!)!.GetColumnName();
                var parentKey = parent.FindPrimaryKey()!.Properties.Single().GetColumnName();
                owned = Scalar(source,
                    $"SELECT COUNT(*) FROM \"{table}\" WHERE \"{key}\" IN (SELECT \"{parentKey}\" FROM \"{parent.GetTableName()}\" WHERE {KnownUser}{(isOptionList ? " OR UserId IS NULL" : string.Empty)});");
            }

            if (total > owned)
            {
                leftBehind[table] = total - owned;
                _logger.LogWarning("{Count} rows of {Table} belong to no user. They are not in any user's database and stay in the backup {Backup}",
                    total - owned, table, BackupPath);
            }

            if (isOptionList && userCount > 0)
            {
                // A shared list is copied to every user who uses it, so there may be more rows than before.
                if (copied < owned)
                {
                    throw new InvalidOperationException($"{table}: {owned} rows belong to users, but only {copied} were copied.");
                }
            }
            else
            {
                Expect(table, userCount == 0 ? 0 : owned, copied);
            }
        }
    }

    /// <summary>The users folder first, then the original becomes the backup and the new platform database takes its place.</summary>
    private void PutInPlace(string work)
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_databases.Folder))
        {
            Directory.Delete(_databases.Folder);
        }

        Directory.Move(Path.Combine(work, UserDatabases.FolderName), _databases.Folder);
        File.Move(_sourcePath, BackupPath);
        File.Move(Path.Combine(work, Path.GetFileName(_sourcePath)), _sourcePath);
    }

    /// <summary>Copies the rows of one table from the original file, column by column as the model names them.</summary>
    private long Copy(SqliteConnection target, IEntityType entityType, string filter, params (string Name, object Value)[] parameters)
    {
        var table = entityType.GetTableName()!;
        var columns = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();

        var sourceColumns = ReadStrings(target, $"SELECT name FROM pragma_table_info('{table}', 'src');");
        if (sourceColumns.Count == 0)
        {
            throw new InvalidOperationException($"The table {table} is missing.");
        }

        var missing = columns.Except(sourceColumns, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"The table {table} lacks the columns {string.Join(", ", missing)}.");
        }

        var list = string.Join(", ", columns.Select(c => $"\"{c}\""));

        return Execute(target, $"INSERT INTO main.\"{table}\" ({list}) SELECT {list} FROM src.\"{table}\" {filter};", parameters);
    }

    private static void Expect(string table, long expected, long copied)
    {
        if (expected != copied)
        {
            throw new InvalidOperationException($"{table}: {expected} rows were expected in the new files, and {copied} were copied.");
        }
    }

    private string ConnectionString(string path, SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate)
    {
        // No pooling: the files are moved as soon as they are complete.
        return new SqliteConnectionStringBuilder(_platform.ConnectionString) { DataSource = path, Mode = mode, Pooling = false }.ToString();
    }

    private SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(ConnectionString(path, mode));
        connection.Open();

        return connection;
    }

    /// <summary>Opens a new file with the original attached as "src". Rows keep their keys, so the foreign keys are checked afterwards instead.</summary>
    private SqliteConnection OpenWithSource(string path)
    {
        var connection = Open(path, SqliteOpenMode.ReadWrite);
        Execute(connection, "PRAGMA foreign_keys = OFF;");
        Execute(connection, "ATTACH DATABASE $source AS src;", ("$source", _sourcePath));

        return connection;
    }

    private static string Quoted(IEnumerable<string> values)
    {
        return string.Join(", ", values.Select(v => $"'{v.Replace("'", "''")}'"));
    }

    private static long Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Command(connection, sql, parameters);

        return command.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Command(connection, sql, parameters);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static List<string> ReadStrings(SqliteConnection connection, string sql)
    {
        using var command = Command(connection, sql, []);
        using var reader = command.ExecuteReader();

        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static SqliteCommand Command(SqliteConnection connection, string sql, (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            // A statement takes only the parameters it names.
            if (sql.Contains(name, StringComparison.Ordinal))
            {
                command.Parameters.AddWithValue(name, value);
            }
        }

        return command;
    }

    private static void DeleteFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
