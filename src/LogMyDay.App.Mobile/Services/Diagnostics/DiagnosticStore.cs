using System.Text;
using LogMyDay.App.Mobile.Services;
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

    // The outbox is one POST per row against an API budget of 100 requests a minute per client.
    // Small batches, at most one flush a minute and a long pause after a 429 keep the diagnostics
    // from starving the app's real calls; pruning keeps a backlog from growing without bound.
    public const int FlushBatchSize = 25;
    public static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan RateLimitedBackoff = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan SyncedRetention = TimeSpan.FromDays(7);
    public static readonly TimeSpan UnsyncedRetention = TimeSpan.FromDays(14);

    private readonly IApiClientProvider _apiClientProvider;
    private readonly ILogger<DiagnosticStore> _logger;
    private readonly object _lock = new();
    private readonly SQLiteConnection _db;

    private bool _enabled;
    private DateTime _nextFlushUtc = DateTime.MinValue;
    private int _flushing;

    public static DiagnosticStore? Instance { get; private set; }

    public bool Enabled => _enabled;

    public DiagnosticStore(IApiClientProvider apiClientProvider, ILogger<DiagnosticStore> logger)
    {
        // NB: resolve the event-log client lazily in FlushAsync, never here — the API client throws
        // "API server not configured" before login, and this store is constructed at startup.
        _apiClientProvider = apiClientProvider;
        _logger = logger;
        _enabled = Preferences.Get(EnabledPrefsKey, false);

        // Ensure the native SQLite provider is registered before opening a connection.
        SQLitePCL.Batteries_V2.Init();

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbFileName);
        _db = new SQLiteConnection(dbPath);
        _db.CreateTable<DiagEventRow>();
        Prune();

        Instance = this;
    }

    /// <summary>
    /// Drops synced rows older than a week and unsynced rows older than two weeks. A row that has
    /// not made it to the server in two weeks never will in any useful way, and keeping it only
    /// makes the next flush longer.
    /// </summary>
    private void Prune()
    {
        try
        {
            var syncedCutoff = (DateTime.UtcNow - SyncedRetention).ToString("o");
            var unsyncedCutoff = (DateTime.UtcNow - UnsyncedRetention).ToString("o");
            int removed;
            lock (_lock)
            {
                removed = _db.Execute("delete from diag_events where (Synced = 1 and TimestampUtc < ?) or (Synced = 0 and TimestampUtc < ?)", syncedCutoff, unsyncedCutoff);
            }

            if (removed > 0)
            {
                Record("diag", $"event=pruned rows={removed}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DiagnosticStore.Prune failed");
        }
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
        if (!_enabled || DateTime.UtcNow < _nextFlushUtc)
        {
            return 0;
        }

        // One flush at a time; a page load and a refresh tick may ask together.
        if (Interlocked.Exchange(ref _flushing, 1) == 1)
        {
            return 0;
        }

        try
        {
            _nextFlushUtc = DateTime.UtcNow + FlushInterval;

            return await FlushBatchAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _flushing, 0);
        }
    }

    private async Task<int> FlushBatchAsync(CancellationToken ct)
    {
        IEventLogApi eventLog;
        try
        {
            eventLog = _apiClientProvider.EventLog;
        }
        catch (Exception ex)
        {
            // Server not configured yet (pre-login) — nothing to flush to. Retry on a later flush.
            _logger.LogDebug(ex, "DiagnosticStore.Flush skipped — API client not ready");

            return 0;
        }

        List<DiagEventRow> pending;
        lock (_lock)
        {
            pending = _db.Table<DiagEventRow>().Where(r => !r.Synced).OrderBy(r => r.Id).Take(FlushBatchSize).ToList();
        }

        var synced = 0;
        foreach (var row in pending)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await eventLog.LogEvent(new EventLogRequest
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
            catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // The server is telling us to stop; the app's own calls share this budget.
                _nextFlushUtc = DateTime.UtcNow + RateLimitedBackoff;
                _logger.LogDebug(ex, "DiagnosticStore.Flush rate limited at row {Id}; backing off", row.Id);

                break;
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
