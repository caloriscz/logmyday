using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NodaTime;
using NodaTime.TimeZones;

namespace LogMyDay.Shared.Preferences;

public static class TimeZoneCatalog
{
    private static readonly Lazy<IReadOnlyList<SelectOption>> timeZones = new(Build);

    public static IReadOnlyList<SelectOption> All => timeZones.Value;

    private static IReadOnlyList<SelectOption> Build()
    {
        var source = TzdbDateTimeZoneSource.Default;
        var locations = source.ZoneLocations?
            .GroupBy(location => location.ZoneId)
            .ToDictionary(group => group.Key, group => group.ToList())
            ?? new Dictionary<string, List<TzdbZoneLocation>>(StringComparer.Ordinal);

        var now = SystemClock.Instance.GetCurrentInstant();

        return DateTimeZoneProviders.Tzdb.Ids
            .Select(id => CreateOption(id, locations, now))
            .OrderBy(option => option.Label, StringComparer.InvariantCultureIgnoreCase)
            .ToArray();
    }

    private static SelectOption CreateOption(string id, IReadOnlyDictionary<string, List<TzdbZoneLocation>> locations, Instant now)
    {
        var zone = DateTimeZoneProviders.Tzdb[id];
        var interval = zone.GetZoneInterval(now);
        var offsetText = FormatOffset(interval.StandardOffset);

    var countryLabel = BuildCountryLabel(id, locations);
        var abbreviation = string.IsNullOrWhiteSpace(interval.Name) ? null : interval.Name;

        string label;
        if (!string.IsNullOrEmpty(countryLabel))
        {
            label = abbreviation is null
                ? $"{countryLabel} — {id} (UTC{offsetText})"
                : $"{countryLabel} — {id} (UTC{offsetText}, {abbreviation})";
        }
        else
        {
            label = abbreviation is null
                ? $"{id} (UTC{offsetText})"
                : $"{id} (UTC{offsetText}, {abbreviation})";
        }

        return new SelectOption(id, label);
    }

    private static string BuildCountryLabel(string id, IReadOnlyDictionary<string, List<TzdbZoneLocation>> locations)
    {
        if (!locations.TryGetValue(id, out var zoneLocations) || zoneLocations.Count == 0)
        {
            return string.Empty;
        }

        var countryNames = zoneLocations
            .Select(location => location.CountryName)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.InvariantCulture)
            .ToArray();

        return string.Join(", ", countryNames);
    }

    private static string FormatOffset(Offset offset)
    {
        if (offset == Offset.Zero)
        {
            return "+00:00";
        }

        var timeSpan = offset.ToTimeSpan();
        var sign = timeSpan < TimeSpan.Zero ? "-" : "+";
        timeSpan = timeSpan.Duration();

        return sign + timeSpan.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }
}
