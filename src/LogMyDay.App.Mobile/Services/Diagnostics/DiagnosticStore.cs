using System.Text;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using SQLite;

namespace LogMyDay.App.Mobile.Services.Diagnostics;

/// <summary>One durable diagnostic event. Written synchronously so it survives Doze, app kill and reboot.</summary>
[Table("diag_events")]
public sealed class DiagEventRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>ISO-8601 round-trip ("o") UTC timestamp.</summary>
    [Indexed]
    public string TimestampUtc { get; set; } = string.Empty;

    /// <summary>Event group, e.g. "reminder-diag". Becomes the "[category]" prefix on the synced server message.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Free-form body, e.g. "event=scheduled itemId=12 ...".</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>True once pushed to the server /event-logs (outbox flag).</summary>
    [Indexed]
    public bool Synced { get; set; }
}

public interface IDiagnosticStore
{
    /// <summary>Whether diagnostic events are being recorded (admin-only).</summary>
    bool Enabled { get; }

    /// <summary>Enable/disable recording for the current account and persist the choice
    /// so out-of-process receivers (alarm, boot) inherit it without an API call.</summary>
    void SetEnabled(bool enabled);

    /// <summary>Synchronously append one diagnostic event. No-op when disabled. Never throws.</summary>
    void Record(string category, string body);

    /// <summary>Push un-synced rows to the server event log. Returns the number synced.</summary>
    Task<int> FlushAsync(CancellationToken ct = default);

    /// <summary>Count of un-synced rows (for the admin diagnostics screen).</summary>
    int PendingCount();

    /// <summary>Write the full store to an NDJSON file in the cache dir and return its path.</summary>
    string ExportToFile();
}

/// <summary>
/// SQLite-backed durable diagnostic log. Admin-gated. Two roles:
///  - records diagnostic events locally (the reliable source of truth), and
///  - an outbox that flushes those events to the server /event-logs.
/// Static <see cref="Instance"/> mirror lets the OS-constructed broadcast receivers
/// (AlarmHandler, BootReceiver — created outside DI) record events too.
/// </summary>
public sealed class DiagnosticStore : IDiagnosticStore
{
    private const string EnabledPrefsKey = "diag.enabled";
    private const string DbFileName = "diagnostics.db3";

    private readonly IEventLogApi _eventLog;
    private readonly ILogger<DiagnosticStore> _logger;
    private readonly object _lock = new();
    private readonly SQLiteConnection _db;

    private bool _enabled;

    public static DiagnosticStore? Instance { get; private set; }

    public bool Enabled => _enabled;

    public DiagnosticStore(IEventLogApi eventLog, ILogger<DiagnosticStore> logger)
    {
        _eventLog = eventLog;
        _logger = logger;
        _enabled = Preferences.Get(EnabledPrefsKey, false);

        // Ensure the native SQLite provider is registered before opening a connection.
        SQLitePCL.Batteries_V2.Init();

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbFileName);
        _db = new SQLiteConnection(dbPath);
        _db.CreateTable<DiagEventRow>();

        Instance = this;
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        Preferences.Set(EnabledPrefsKey, enabled);
    }

    public void Record(string category, string body)
    {
        if (!_enabled)
        {
            return;
        }

        // Best-effort: a diagnostics failure must never disrupt the app's real work.
        try
        {
            var row = new DiagEventRow
            {
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                Category = category,
                Body = body,
                Synced = false
            };

            lock (_lock)
            {
                _db.Insert(row);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DiagnosticStore.Record failed for [{Category}] {Body}", category, body);
        }
    }

    public async Task<int> FlushAsync(CancellationToken ct = default)
    {
        if (!_enabled)
        {
            return 0;
        }

        List<DiagEventRow> pending;
        lock (_lock)
        {
            pending = _db.Table<DiagEventRow>().Where(r => !r.Synced).OrderBy(r => r.Id).Take(200).ToList();
        }

        var synced = 0;
        foreach (var row in pending)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await _eventLog.LogEvent(new EventLogRequest
                {
                    Level = "Info",
                    Message = $"[{row.Category}] {row.Body}"
                }).ConfigureAwait(false);

                row.Synced = true;
                lock (_lock)
                {
                    _db.Update(row);
                }

                synced++;
            }
            catch (Exception ex)
            {
                // Leave the row un-synced; a later flush retries it. Stop on first failure
                // (likely offline / auth) rather than hammering a dead path.
                _logger.LogDebug(ex, "DiagnosticStore.Flush stopped at row {Id}", row.Id);

                break;
            }
        }

        return synced;
    }

    public int PendingCount()
    {
        lock (_lock)
        {
            return _db.Table<DiagEventRow>().Count(r => !r.Synced);
        }
    }

    public string ExportToFile()
    {
        List<DiagEventRow> rows;
        lock (_lock)
        {
            rows = _db.Table<DiagEventRow>().OrderBy(r => r.Id).ToList();
        }

        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            // NDJSON — one self-describing record per line, easy to diff against /event-logs.
            var safeBody = row.Body.Replace("\\", "\\\\").Replace("\"", "\\\"");
            sb.Append("{\"ts\":\"").Append(row.TimestampUtc)
              .Append("\",\"category\":\"").Append(row.Category)
              .Append("\",\"synced\":").Append(row.Synced ? "true" : "false")
              .Append(",\"body\":\"").Append(safeBody).Append("\"}\n");
        }

        var path = Path.Combine(FileSystem.CacheDirectory, $"diag-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.ndjson");
        File.WriteAllText(path, sb.ToString());

        return path;
    }
}
