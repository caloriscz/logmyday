using System.Text.Json;
using Microsoft.Maui.Storage;

namespace LogMyDay.App.Mobile.Services.Diagnostics;

/// <summary>One armed reminder alarm, persisted so it can be re-created after a reboot.</summary>
public sealed record ArmedAlarm(int ItemId, DateTime FireAtUtc, string Title, string? Notes);

/// <summary>
/// Durable snapshot of the alarms currently armed on this device, kept in <see cref="Preferences"/>
/// as JSON. Android clears every scheduled alarm on reboot; the mobile app holds credentials in
/// memory only, so after a reboot it cannot re-fetch reminders from the API. This snapshot lets the
/// boot receiver re-arm the previously-scheduled alarms with no network and no login.
///
/// Not gated to admins — re-arming after reboot is a reliability fix for all users. Holds no
/// credentials, only reminder ids/times/titles already present on the device.
/// </summary>
public static class ArmedAlarmStore
{
    private const string Key = "reminder.armed-snapshot";
    private static readonly object Lock = new();

    public static IReadOnlyList<ArmedAlarm> GetAll()
    {
        var raw = Preferences.Get(Key, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<ArmedAlarm>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ArmedAlarm>>(raw) ?? new List<ArmedAlarm>();
        }
        catch (JsonException)
        {
            return Array.Empty<ArmedAlarm>();
        }
    }

    public static void Upsert(ArmedAlarm alarm)
    {
        lock (Lock)
        {
            var list = GetAll().Where(a => a.ItemId != alarm.ItemId).ToList();
            list.Add(alarm);
            Save(list);
        }
    }

    public static void Remove(int itemId)
    {
        lock (Lock)
        {
            var list = GetAll().Where(a => a.ItemId != itemId).ToList();
            Save(list);
        }
    }

    private static void Save(List<ArmedAlarm> list)
    {
        Preferences.Set(Key, JsonSerializer.Serialize(list));
    }
}
