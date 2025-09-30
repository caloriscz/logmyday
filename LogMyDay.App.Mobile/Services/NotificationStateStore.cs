using System.Linq;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace LogMyDay.App.Mobile.Services;

public interface INotificationStateStore
{
    NotificationStateSnapshot GetState(int notificationId, DateOnly baseDate);
    void SaveState(int notificationId, NotificationStateSnapshot snapshot);
    void RemoveObsoleteNotifications(IEnumerable<int> activeNotificationIds);
    void PruneOlderThan(DateOnly threshold);
    void ClearAll();
}

public sealed record NotificationStateSnapshot(DateOnly BaseDate, int NudgesSent, DateTime? LastSentUtc)
{
    public NotificationStateSnapshot IncrementNudges(DateTime sentUtc)
        => this with { NudgesSent = NudgesSent + 1, LastSentUtc = sentUtc };
}

internal sealed class NotificationStateStore : INotificationStateStore
{
    private const string PreferencesKey = "notification_state_v1";
    private readonly object _sync = new();
    private Dictionary<string, NotificationStateRecord> _cache = new();
    private bool _loaded;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false
    };

    public NotificationStateSnapshot GetState(int notificationId, DateOnly baseDate)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var key = BuildKey(notificationId, baseDate);
            if (_cache.TryGetValue(key, out var record))
            {
                return record.ToSnapshot(baseDate);
            }

            return new NotificationStateSnapshot(baseDate, 0, null);
        }
    }

    public void SaveState(int notificationId, NotificationStateSnapshot snapshot)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var key = BuildKey(notificationId, snapshot.BaseDate);
            _cache[key] = NotificationStateRecord.FromSnapshot(snapshot);
            Persist();
        }
    }

    public void RemoveObsoleteNotifications(IEnumerable<int> activeNotificationIds)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var active = new HashSet<int>(activeNotificationIds);
            if (active.Count == 0 && _cache.Count == 0)
            {
                return;
            }

            var toRemove = _cache
                .Where(pair => !active.Contains(ParseNotificationId(pair.Key)))
                .Select(pair => pair.Key)
                .ToList();

            if (toRemove.Count == 0)
            {
                return;
            }

            foreach (var key in toRemove)
            {
                _cache.Remove(key);
            }

            Persist();
        }
    }

    public void PruneOlderThan(DateOnly threshold)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var toRemove = _cache
                .Where(pair => TryParseBaseDate(pair.Key, out var baseDate) && baseDate < threshold)
                .Select(pair => pair.Key)
                .ToList();

            if (toRemove.Count == 0)
            {
                return;
            }

            foreach (var key in toRemove)
            {
                _cache.Remove(key);
            }

            Persist();
        }
    }

    public void ClearAll()
    {
        lock (_sync)
        {
            _cache = new Dictionary<string, NotificationStateRecord>();
            _loaded = true;
            Preferences.Remove(PreferencesKey);
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

    var stored = Preferences.Get(PreferencesKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, NotificationStateRecord>>(stored, _serializerOptions);
                if (parsed != null)
                {
                    _cache = parsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NotificationStateStore: Failed to deserialize state - {ex.Message}");
                _cache = new Dictionary<string, NotificationStateRecord>();
            }
        }

        _loaded = true;
    }

    private void Persist()
    {
        try
        {
            var serialized = JsonSerializer.Serialize(_cache, _serializerOptions);
            Preferences.Set(PreferencesKey, serialized);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NotificationStateStore: Failed to persist state - {ex.Message}");
        }
    }

    private static string BuildKey(int notificationId, DateOnly baseDate)
        => $"{notificationId}:{baseDate:yyyyMMdd}";

    private static int ParseNotificationId(string key)
    {
        var colonIndex = key.IndexOf(':');
        if (colonIndex <= 0)
        {
            return -1;
        }

        if (int.TryParse(key.AsSpan(0, colonIndex), out var value))
        {
            return value;
        }

        return -1;
    }

    private static bool TryParseBaseDate(string key, out DateOnly date)
    {
        var colonIndex = key.IndexOf(':');
        if (colonIndex <= 0 || colonIndex + 1 >= key.Length)
        {
            date = default;
            return false;
        }

        var span = key.AsSpan(colonIndex + 1);
        if (DateOnly.TryParseExact(span, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }

        date = default;
        return false;
    }

    private sealed class NotificationStateRecord
    {
        public string BaseDate { get; set; } = string.Empty;
        public int NudgesSent { get; set; }
        public DateTime? LastSentUtc { get; set; }

        public NotificationStateSnapshot ToSnapshot(DateOnly baseDate)
            => new(baseDate, NudgesSent, LastSentUtc);

        public static NotificationStateRecord FromSnapshot(NotificationStateSnapshot snapshot)
            => new()
            {
                BaseDate = snapshot.BaseDate.ToString("yyyyMMdd"),
                NudgesSent = snapshot.NudgesSent,
                LastSentUtc = snapshot.LastSentUtc
            };
    }
}
